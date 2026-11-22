using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CCJ.Models;

namespace CCJ.Services
{
    /// <summary>
    /// Reports whether Windows Credential Manager holds an entry for each host. It reads target
    /// names only. No password is ever read, decrypted, shown or stored by this app.
    /// </summary>
    public class CredentialChecker
    {
        private const int CRED_ENUMERATE_ALL_CREDENTIALS = 0x1;
        private const int ERROR_NOT_FOUND = 1168;
        private const int ERROR_INVALID_FLAGS = 1004;

        [DllImport("advapi32.dll", EntryPoint = "CredEnumerateW", CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern bool CredEnumerate(
            string filter, int flags, out int count, out IntPtr credentialsPtr);

        [DllImport("advapi32.dll", EntryPoint = "CredFree")]
        private static extern void CredFree(IntPtr buffer);

        /// <summary>
        /// Only the fields we need. The credential blob is deliberately never touched, so no
        /// secret is ever marshalled into this process.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public uint Flags;
            public uint Type;
            public IntPtr TargetName;
            public IntPtr Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public IntPtr UserName;
        }

        /// <summary>One result per distinct host used by the jobs.</summary>
        public List<CheckResult> CheckHosts(JobsFile file)
        {
            List<CheckResult> results = new List<CheckResult>();
            if (file == null) return results;

            List<string> hosts = new List<string>();
            foreach (JobConfig job in file.Jobs)
            {
                foreach (string path in new string[] { job.SourcePath, job.NasRoot })
                {
                    string host = ScriptGenerator.HostOf(path);
                    if (host.Length == 0) continue;

                    bool known = false;
                    foreach (string seen in hosts)
                    {
                        if (string.Equals(seen, host, StringComparison.OrdinalIgnoreCase)) known = true;
                    }
                    if (!known) hosts.Add(host);
                }
            }

            if (hosts.Count == 0)
            {
                results.Add(new CheckResult("Nothing to check", false, "No host is set on any job."));
                return results;
            }

            List<string> targets;
            string source;
            try
            {
                targets = ListTargetNames(out source);
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not read Credential Manager.", ex);
                results.Add(new CheckResult("Credential Manager", false,
                    "could not be read: " + ex.Message));
                return results;
            }

            foreach (string host in hosts)
            {
                List<string> matches = new List<string>();
                foreach (string target in targets)
                {
                    if (target.IndexOf(host, StringComparison.OrdinalIgnoreCase) >= 0)
                        matches.Add(target);
                }

                if (matches.Count > 0)
                {
                    results.Add(new CheckResult("Saved credential for " + host, true,
                        "found (" + string.Join(", ", matches.ToArray()) + ")"));
                }
                else
                {
                    results.Add(new CheckResult("Saved credential for " + host, false,
                        "no entry found. Add one in Credential Manager, or the scheduled task " +
                        "will not be able to reach this host."));
                }
            }

            results.Add(new CheckResult("Read via", true, source +
                ". Passwords are never read or displayed by CCJ."));
            return results;
        }

        /// <summary>
        /// Target names of the current user's stored credentials. Tries the API first and falls
        /// back to cmdkey, which prints target names only.
        /// </summary>
        public List<string> ListTargetNames(out string source)
        {
            try
            {
                List<string> viaApi = EnumerateViaApi();
                source = "CredEnumerate (Windows Credential Manager)";
                return viaApi;
            }
            catch (Exception ex)
            {
                AppLog.Warn("CredEnumerate failed (" + ex.Message + "), falling back to cmdkey /list.");
                source = "cmdkey /list";
                return EnumerateViaCmdKey();
            }
        }

        private static List<string> EnumerateViaApi()
        {
            int count;
            IntPtr array;

            bool ok = CredEnumerate(null, 0, out count, out array);
            if (!ok)
            {
                int error = Marshal.GetLastWin32Error();

                if (error == ERROR_NOT_FOUND)
                    return new List<string>();

                if (error == ERROR_INVALID_FLAGS)
                {
                    ok = CredEnumerate(null, CRED_ENUMERATE_ALL_CREDENTIALS, out count, out array);
                    if (!ok)
                    {
                        error = Marshal.GetLastWin32Error();
                        if (error == ERROR_NOT_FOUND) return new List<string>();
                        throw new System.ComponentModel.Win32Exception(error);
                    }
                }
                else
                {
                    throw new System.ComponentModel.Win32Exception(error);
                }
            }

            List<string> targets = new List<string>();
            try
            {
                for (int i = 0; i < count; i++)
                {
                    IntPtr entry = Marshal.ReadIntPtr(array, i * IntPtr.Size);
                    if (entry == IntPtr.Zero) continue;

                    CREDENTIAL credential = (CREDENTIAL)Marshal.PtrToStructure(entry, typeof(CREDENTIAL));
                    if (credential.TargetName == IntPtr.Zero) continue;

                    string target = Marshal.PtrToStringUni(credential.TargetName);
                    if (!string.IsNullOrEmpty(target))
                        targets.Add(target);
                }
            }
            finally
            {
                if (array != IntPtr.Zero) CredFree(array);
            }
            return targets;
        }

        private static List<string> EnumerateViaCmdKey()
        {
            List<string> targets = new List<string>();

            ProcessStartInfo info = new ProcessStartInfo("cmdkey.exe", "/list");
            info.UseShellExecute = false;
            info.RedirectStandardOutput = true;
            info.CreateNoWindow = true;
            info.StandardOutputEncoding = Encoding.Default;

            using (Process process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(10000);

                string[] lines = output.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    int colon = trimmed.IndexOf(':');
                    if (colon <= 0) continue;

                    string label = trimmed.Substring(0, colon).Trim();
                    // "Target" in English, "Cible" in French. Match on the known labels only.
                    if (label.Equals("Target", StringComparison.OrdinalIgnoreCase) ||
                        label.Equals("Cible", StringComparison.OrdinalIgnoreCase))
                    {
                        string value = trimmed.Substring(colon + 1).Trim();
                        if (value.Length > 0) targets.Add(value);
                    }
                }
            }
            return targets;
        }

        /// <summary>Opens the Credential Manager control panel page.</summary>
        public static void OpenCredentialManager()
        {
            ProcessStartInfo info = new ProcessStartInfo("control.exe", "/name Microsoft.CredentialManager");
            info.UseShellExecute = true;
            Process.Start(info);
        }
    }
}
