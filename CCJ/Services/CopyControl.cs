using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace CCJ.Services
{
    /// <summary>One robocopy process that belongs to a job's run.</summary>
    public class RunningCopy
    {
        public RunningCopy(int processId, string commandLine)
        {
            ProcessId = processId;
            CommandLine = commandLine == null ? "" : commandLine;
        }

        public int ProcessId { get; private set; }
        public string CommandLine { get; private set; }
    }

    /// <summary>What happened when a pause, resume or stop was asked for.</summary>
    public class ControlResult
    {
        public ControlResult(int found, int affected, string error)
        {
            Found = found;
            Affected = affected;
            Error = error;
        }

        /// <summary>How many robocopy processes of this job were found.</summary>
        public int Found { get; private set; }

        /// <summary>How many of them the action actually worked on.</summary>
        public int Affected { get; private set; }

        /// <summary>Why it did not work, or null when it did.</summary>
        public string Error { get; private set; }

        public bool Ok { get { return Error == null && Affected > 0; } }
    }

    /// <summary>
    /// Pauses, resumes and stops the robocopy process of a running backup.
    ///
    /// The scripts are not changed and robocopy has no pause of its own, so a pause is the
    /// operating system's: every thread of that robocopy process is suspended, and resumed again
    /// later. robocopy itself notices nothing. Nothing is ever deleted here, and a stopped run
    /// leaves the files it had already copied in place.
    ///
    /// The right process is found by the log file in robocopy's own command line, which is unique
    /// to one run of one job. That is also what keeps a dry run, whose log lives under TEMP, from
    /// being mistaken for the real run of the same job.
    /// </summary>
    public static class CopyControl
    {
        private const int ProcessSuspendResume = 0x0800;

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint NtSuspendProcess(IntPtr processHandle);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint NtResumeProcess(IntPtr processHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(int access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        /// <summary>
        /// True when this robocopy command line belongs to the run described by the log file, or,
        /// while the log file is not known yet, to a run logging into that folder for that NAS
        /// folder name.
        /// </summary>
        public static bool MatchesJob(string commandLine, string logPath, string logsDir, string folder)
        {
            if (string.IsNullOrEmpty(commandLine)) return false;

            if (!string.IsNullOrEmpty(logPath))
                return Contains(commandLine, logPath);

            if (string.IsNullOrEmpty(logsDir) || string.IsNullOrEmpty(folder)) return false;

            return Contains(commandLine, logsDir.TrimEnd('\\')) &&
                   Contains(commandLine, "\\" + folder.Trim() + "_");
        }

        /// <summary>
        /// Every robocopy process that belongs to this run. An empty list means the copy is not at
        /// the robocopy stage: the script may still be checking the shares or waiting to retry.
        /// </summary>
        public static List<RunningCopy> Find(string logPath, string logsDir, string folder)
        {
            List<RunningCopy> found = new List<RunningCopy>();

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'robocopy.exe'"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject row in results)
                    {
                        using (row)
                        {
                            string commandLine = Convert.ToString(row["CommandLine"]);
                            if (!MatchesJob(commandLine, logPath, logsDir, folder)) continue;

                            int pid = Convert.ToInt32(row["ProcessId"]);
                            found.Add(new RunningCopy(pid, commandLine));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not list the running robocopy processes.", ex);
            }

            return found;
        }

        public static ControlResult Pause(List<RunningCopy> copies)
        {
            return Each(copies, true);
        }

        public static ControlResult Resume(List<RunningCopy> copies)
        {
            return Each(copies, false);
        }

        /// <summary>
        /// Ends the robocopy processes. Files already copied are left where they are; robocopy is
        /// never asked to delete anything, and killing it cannot remove what is on the destination.
        /// </summary>
        public static ControlResult Terminate(List<RunningCopy> copies)
        {
            if (copies == null || copies.Count == 0)
                return new ControlResult(0, 0, "no robocopy process of this job is running.");

            int done = 0;
            string error = null;

            foreach (RunningCopy copy in copies)
            {
                try
                {
                    using (Process process = Process.GetProcessById(copy.ProcessId))
                    {
                        process.Kill();
                        done++;
                    }
                }
                catch (ArgumentException)
                {
                    // It ended between being listed and being killed, which is the wanted result.
                    done++;
                }
                catch (Exception ex)
                {
                    AppLog.Error("Could not end robocopy process " + copy.ProcessId + ".", ex);
                    if (error == null) error = ex.Message;
                }
            }

            return new ControlResult(copies.Count, done, done > 0 ? null : error);
        }

        private static ControlResult Each(List<RunningCopy> copies, bool suspend)
        {
            if (copies == null || copies.Count == 0)
            {
                return new ControlResult(0, 0,
                    "no robocopy process of this job is running. The script may be checking the " +
                    "shares or waiting before its next attempt; there is nothing to " +
                    (suspend ? "pause" : "resume") + " yet.");
            }

            int done = 0;
            string error = null;

            foreach (RunningCopy copy in copies)
            {
                IntPtr handle = OpenProcess(ProcessSuspendResume, false, copy.ProcessId);
                if (handle == IntPtr.Zero)
                {
                    int code = Marshal.GetLastWin32Error();
                    string message = new System.ComponentModel.Win32Exception(code).Message;
                    AppLog.Warn("Could not open robocopy process " + copy.ProcessId + ": " + message);

                    if (error == null)
                    {
                        error = message + " The backup task may be running as another account, " +
                                "in which case CCJ has to be restarted as administrator.";
                    }
                    continue;
                }

                try
                {
                    uint status = suspend ? NtSuspendProcess(handle) : NtResumeProcess(handle);
                    if (status == 0)
                    {
                        done++;
                    }
                    else
                    {
                        AppLog.Warn((suspend ? "Suspending" : "Resuming") + " robocopy process " +
                                    copy.ProcessId + " returned status 0x" + status.ToString("X8") + ".");
                        if (error == null)
                            error = "Windows refused to " + (suspend ? "suspend" : "resume") +
                                    " the copy (status 0x" + status.ToString("X8") + ").";
                    }
                }
                finally
                {
                    CloseHandle(handle);
                }
            }

            if (done > 0)
            {
                AppLog.Info((suspend ? "Suspended " : "Resumed ") + done + " robocopy process(es).");
                return new ControlResult(copies.Count, done, null);
            }
            return new ControlResult(copies.Count, 0, error);
        }

        private static bool Contains(string text, string part)
        {
            return text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
