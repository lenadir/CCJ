using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CCJ.Setup
{
    /// <summary>
    /// Single file installer for CCJ. Everything it installs is embedded in this exe, so there is
    /// one file to hand over and nothing to download.
    ///
    ///   CCJ-Setup.exe                     the window: pick folders, press Install
    ///   CCJ-Setup.exe /S                  silent install with the default folders
    ///   CCJ-Setup.exe /S /dir=... /vox=... silent install into chosen folders
    ///   CCJ-Setup.exe /uninstall          remove it again
    ///
    /// Targets .NET Framework 4.0 so that it can still start on a machine that has an older 4.x
    /// and tell the user, in plain words, that 4.8 is needed.
    /// </summary>
    internal static class Program
    {
        public const string Product = "CCJ";
        public const string Version = "1.0.0";
        public const string ResourcePrefix = "CCJ.Setup.Payload.";
        public const string RegistryUninstallPath =
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CCJ";

        /// <summary>.NET Framework 4.8 is release 528040.</summary>
        public const int RequiredFrameworkRelease = 528040;

        /// <summary>Files carried inside this exe, in the order they are written.</summary>
        public static readonly string[] Payload = new string[]
        {
            "CCJ.exe",
            "CCJ.exe.config",
            "Microsoft.Win32.TaskScheduler.dll",
            "Newtonsoft.Json.dll",
            "USER-MANUAL.md",
            "README.md",
            "INSTALL.md"
        };

        public static string DefaultInstallDir
        {
            get
            {
                string programFiles = Environment.GetEnvironmentVariable("ProgramW6432");
                if (string.IsNullOrEmpty(programFiles))
                    programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                return Path.Combine(programFiles, Product);
            }
        }

        public const string DefaultVoxRoot = @"C:\Vox";

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool silent = HasFlag(args, "/S") || HasFlag(args, "/silent");
            bool uninstall = HasFlag(args, "/uninstall") || HasFlag(args, "/u");

            try
            {
                if (uninstall)
                    return Uninstaller.Run(silent);

                if (silent)
                    return SilentInstall(args);

                Application.Run(new SetupForm());
                return 0;
            }
            catch (Exception ex)
            {
                if (silent)
                {
                    Console.Error.WriteLine(ex.Message);
                    return 1;
                }
                MessageBox.Show("Setup could not finish.\r\n\r\n" + ex.Message,
                    "CCJ Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private static int SilentInstall(string[] args)
        {
            string installDir = ValueOf(args, "/dir=", DefaultInstallDir);
            string voxRoot = ValueOf(args, "/vox=", DefaultVoxRoot);
            bool shortcuts = !HasFlag(args, "/noshortcut");

            Engine.Install(installDir, voxRoot, shortcuts, shortcuts,
                new Action<string>(Console.WriteLine));
            return 0;
        }

        /// <summary>Reads "/dir=C:\Somewhere" style options, quoted or not.</summary>
        private static string ValueOf(string[] args, string prefix, string fallback)
        {
            if (args == null) return fallback;
            foreach (string arg in args)
            {
                if (arg != null && arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string value = arg.Substring(prefix.Length).Trim().Trim('"');
                    if (value.Length > 0) return value;
                }
            }
            return fallback;
        }

        private static bool HasFlag(string[] args, string flag)
        {
            if (args == null) return false;
            foreach (string arg in args)
            {
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(arg, "-" + flag.TrimStart('/', '-'), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }

    /// <summary>Everything the installer actually does, with no user interface attached.</summary>
    internal static class Engine
    {
        public static void Install(string installDir, string voxRoot, bool desktopShortcut,
                                   bool startMenuShortcut, Action<string> log)
        {
            if (string.IsNullOrEmpty(installDir)) throw new ArgumentException("No install folder given.");
            if (string.IsNullOrEmpty(voxRoot)) throw new ArgumentException("No script and log folder given.");

            CheckFramework(log);

            log("Creating " + installDir);
            Directory.CreateDirectory(installDir);

            foreach (string name in Program.Payload)
            {
                string target = Path.Combine(installDir, name);
                log("  writing " + name);
                WriteResource(Program.ResourcePrefix + name, target);
            }

            log("Creating " + Path.Combine(voxRoot, "Logs"));
            Directory.CreateDirectory(Path.Combine(voxRoot, "Logs"));

            log("Installing the uninstaller");
            string uninstaller = Path.Combine(installDir, "Uninstall-CCJ.exe");
            CopySelf(uninstaller);

            if (startMenuShortcut)
            {
                string startMenu = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                    "Programs");
                log("Start menu shortcut");
                CreateShortcut(Path.Combine(startMenu, "CCJ.lnk"),
                    Path.Combine(installDir, "CCJ.exe"), installDir,
                    "Manage the Robocopy backup jobs");
            }

            if (desktopShortcut)
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                log("Desktop shortcut");
                CreateShortcut(Path.Combine(desktop, "CCJ.lnk"),
                    Path.Combine(installDir, "CCJ.exe"), installDir,
                    "Manage the Robocopy backup jobs");
            }

            try
            {
                RegisterUninstall(installDir, uninstaller);
                log("Registered in Apps and Features");
            }
            catch (Exception ex)
            {
                // Worth reporting, but the install itself is already usable without it.
                log("Could not register in Apps and Features: " + ex.Message);
                log("  (uninstall with " + uninstaller + " /uninstall)");
            }

            log("");
            log("Done. CCJ is installed in " + installDir);
        }

        /// <summary>
        /// The installer runs on .NET 4.0 or later, but CCJ itself needs 4.8. Checking here gives
        /// a clear message instead of a crash the first time CCJ.exe is started.
        /// </summary>
        public static void CheckFramework(Action<string> log)
        {
            int release = 0;
            try
            {
                using (RegistryKey key = RegistryKey
                    .OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("Release");
                        if (value != null) release = Convert.ToInt32(value);
                    }
                }
            }
            catch (Exception)
            {
            }

            if (release >= Program.RequiredFrameworkRelease)
            {
                log("Found .NET Framework 4.8 or later (release " + release + ")");
                return;
            }

            throw new InvalidOperationException(
                "CCJ needs .NET Framework 4.8, which was not found on this PC" +
                (release > 0 ? " (release " + release + " is installed)" : "") + ".\r\n\r\n" +
                "Windows 10 version 21H2 and later include it. On an older build, install the " +
                ".NET Framework 4.8 Runtime from Microsoft, then run this setup again.");
        }

        private static void WriteResource(string resourceName, string targetPath)
        {
            Assembly assembly = typeof(Engine).Assembly;
            using (Stream source = assembly.GetManifestResourceStream(resourceName))
            {
                if (source == null)
                    throw new InvalidOperationException("This setup is incomplete: " + resourceName + " is missing.");

                string dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                // A running CCJ.exe cannot be overwritten, so say so plainly.
                try
                {
                    using (FileStream target = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                    {
                        byte[] buffer = new byte[81920];
                        int read;
                        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                            target.Write(buffer, 0, read);
                    }
                }
                catch (IOException ex)
                {
                    throw new IOException(
                        "Could not write " + targetPath + ". If CCJ is running, close it and try again.\r\n\r\n" +
                        ex.Message, ex);
                }
            }
        }

        private static void CopySelf(string targetPath)
        {
            string self = Process.GetCurrentProcess().MainModule.FileName;
            if (string.Equals(Path.GetFullPath(self), Path.GetFullPath(targetPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            File.Copy(self, targetPath, true);
        }

        /// <summary>Uses WScript.Shell late bound, so the installer needs no COM reference.</summary>
        public static void CreateShortcut(string linkPath, string targetPath, string workingDir,
                                          string description)
        {
            try
            {
                string dir = Path.GetDirectoryName(linkPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;

                object shell = Activator.CreateInstance(shellType);
                object link = shellType.InvokeMember("CreateShortcut",
                    BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });

                Type linkType = link.GetType();
                linkType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link,
                    new object[] { targetPath });
                linkType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link,
                    new object[] { workingDir });
                linkType.InvokeMember("Description", BindingFlags.SetProperty, null, link,
                    new object[] { description });
                linkType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, link,
                    new object[] { targetPath + ",0" });
                linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
            }
            catch (Exception)
            {
                // A missing shortcut is not worth failing the whole install over.
            }
        }

        private static void RegisterUninstall(string installDir, string uninstaller)
        {
            long size = 0;
            try
            {
                foreach (string file in Directory.GetFiles(installDir))
                    size += new FileInfo(file).Length;
            }
            catch (Exception)
            {
            }

            using (RegistryKey key = RegistryKey
                .OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .CreateSubKey(Program.RegistryUninstallPath))
            {
                if (key == null) return;

                key.SetValue("DisplayName", "CCJ - Robocopy backup jobs");
                key.SetValue("DisplayVersion", Program.Version);
                key.SetValue("Publisher", "CCJ");
                key.SetValue("InstallLocation", installDir);
                key.SetValue("DisplayIcon", Path.Combine(installDir, "CCJ.exe"));
                key.SetValue("UninstallString", "\"" + uninstaller + "\" /uninstall");
                key.SetValue("QuietUninstallString", "\"" + uninstaller + "\" /uninstall /S");
                key.SetValue("EstimatedSize", (int)(size / 1024), RegistryValueKind.DWord);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
    }

    internal static class Uninstaller
    {
        public static int Run(bool silent)
        {
            if (!silent)
            {
                DialogResult answer = MessageBox.Show(
                    "Remove CCJ from this PC?\r\n\r\n" +
                    "The three \"CCJ Backup\" scheduled tasks are removed as well.\r\n\r\n" +
                    "Your logs and generated scripts in the script and log folder are KEPT, and " +
                    "nothing on the NAS is touched.",
                    "Uninstall CCJ", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (answer != DialogResult.Yes) return 0;
            }

            string installDir = ReadInstallDir();
            StringBuilder report = new StringBuilder();

            int tasks = RemoveScheduledTasks();
            report.AppendLine(tasks + " scheduled task(s) removed.");

            RemoveShortcut(Path.Combine(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"), "CCJ.lnk"));
            RemoveShortcut(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "CCJ.lnk"));
            report.AppendLine("Shortcuts removed.");

            try
            {
                RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .DeleteSubKeyTree(Program.RegistryUninstallPath, false);
                report.AppendLine("Removed from Apps and Features.");
            }
            catch (Exception)
            {
            }

            if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
            {
                ScheduleFolderDelete(installDir);
                report.AppendLine("Program files will be removed in a few seconds.");
            }

            report.AppendLine();
            report.AppendLine("Kept on purpose: your settings in %APPDATA%\\CCJ, and the logs and " +
                              "scripts in the script and log folder.");

            if (!silent)
            {
                MessageBox.Show(report.ToString(), "CCJ uninstalled",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return 0;
        }

        private static string ReadInstallDir()
        {
            try
            {
                using (RegistryKey key = RegistryKey
                    .OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey(Program.RegistryUninstallPath))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("InstallLocation");
                        if (value != null) return Convert.ToString(value);
                    }
                }
            }
            catch (Exception)
            {
            }

            return Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
        }

        /// <summary>Uses schtasks.exe so the uninstaller carries no dependencies of its own.</summary>
        private static int RemoveScheduledTasks()
        {
            int removed = 0;
            try
            {
                ProcessStartInfo query = new ProcessStartInfo("schtasks.exe", "/Query /FO CSV /NH");
                query.UseShellExecute = false;
                query.RedirectStandardOutput = true;
                query.CreateNoWindow = true;

                string output;
                using (Process process = Process.Start(query))
                {
                    output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(30000);
                }

                List<string> names = new List<string>();
                string[] lines = output.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    if (!line.StartsWith("\"")) continue;
                    int closing = line.IndexOf('"', 1);
                    if (closing <= 1) continue;

                    string name = line.Substring(1, closing - 1);
                    if (name.IndexOf("CCJ Backup", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        !names.Contains(name))
                    {
                        names.Add(name);
                    }
                }

                foreach (string name in names)
                {
                    ProcessStartInfo delete = new ProcessStartInfo(
                        "schtasks.exe", "/Delete /TN \"" + name + "\" /F");
                    delete.UseShellExecute = false;
                    delete.CreateNoWindow = true;

                    using (Process process = Process.Start(delete))
                    {
                        process.WaitForExit(30000);
                        if (process.ExitCode == 0) removed++;
                    }
                }
            }
            catch (Exception)
            {
            }
            return removed;
        }

        private static void RemoveShortcut(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// The uninstaller lives in the folder it has to delete, so the delete is handed to a
        /// short lived cmd that waits for this process to exit first.
        /// </summary>
        private static void ScheduleFolderDelete(string dir)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo("cmd.exe",
                    "/c ping -n 4 127.0.0.1 >nul & rd /s /q \"" + dir + "\"");
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                Process.Start(info);
            }
            catch (Exception)
            {
            }
        }
    }
}
