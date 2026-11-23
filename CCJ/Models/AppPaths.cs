using System;
using System.IO;

namespace CCJ.Models
{
    /// <summary>
    /// Every path the application uses. Nothing else in the code builds these paths by hand.
    /// </summary>
    public static class AppPaths
    {
        public const string AppFolderName = "CCJ";

        /// <summary>
        /// Setting this environment variable moves jobs.json and app.log somewhere else. The unit
        /// tests use it so they never touch the real settings, and it allows a portable setup.
        /// </summary>
        public const string DataDirVariable = "CCJ_DATA_DIR";

        /// <summary>%APPDATA%\CCJ, unless CCJ_DATA_DIR says otherwise.</summary>
        public static string DataDir
        {
            get
            {
                string custom = Environment.GetEnvironmentVariable(DataDirVariable);
                if (!string.IsNullOrEmpty(custom)) return custom;

                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    AppFolderName);
            }
        }

        /// <summary>%APPDATA%\CCJ\jobs.json</summary>
        public static string JobsFilePath { get { return Path.Combine(DataDir, "jobs.json"); } }

        /// <summary>%APPDATA%\CCJ\ui.json, which only remembers how the window should look.</summary>
        public static string UiSettingsPath { get { return Path.Combine(DataDir, "ui.json"); } }

        /// <summary>%APPDATA%\CCJ\app.log</summary>
        public static string AppLogPath { get { return Path.Combine(DataDir, "app.log"); } }

        /// <summary>Folder used for every dry run. Always under %TEMP%, never the Vox root.</summary>
        public static string DryRunDir { get { return Path.Combine(Path.GetTempPath(), "CCJ_DryRun"); } }

        public static string ExeDir { get { return AppDomain.CurrentDomain.BaseDirectory; } }

        /// <summary>
        /// Best guess at the Vox root the first time the app runs. It is written to jobs.json on
        /// the first save, after which the stored value wins and the user can change it.
        /// </summary>
        public static string GuessVoxRoot()
        {
            string dir = ExeDir;

            // A "Vox" folder beside the exe or one, two or three levels up. That covers running
            // from dist\ as well as from CCJ\bin\Release\.
            for (int up = 0; up <= 3; up++)
            {
                string candidate = Path.GetFullPath(Path.Combine(dir, Repeat("..", up) + "Vox"));
                if (Directory.Exists(candidate))
                    return candidate;
            }

            // The exe sitting directly inside a deployed Vox root.
            if (File.Exists(Path.Combine(dir, "Backup_Folder1.bat")) ||
                Directory.Exists(Path.Combine(dir, "Logs")))
            {
                return Path.GetFullPath(dir);
            }

            if (Directory.Exists(@"C:\Vox"))
                return @"C:\Vox";

            return Path.GetFullPath(Path.Combine(dir, "Vox"));
        }

        private static string Repeat(string value, int count)
        {
            string result = "";
            for (int i = 0; i < count; i++)
                result = result + value + Path.DirectorySeparatorChar;
            return result;
        }

        public static void EnsureDataDir()
        {
            if (!Directory.Exists(DataDir))
                Directory.CreateDirectory(DataDir);
        }
    }
}
