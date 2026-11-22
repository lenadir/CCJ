using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CCJ.Services
{
    public class PlanResult
    {
        /// <summary>How many files robocopy would copy.</summary>
        public long Files { get; set; }

        /// <summary>How many bytes robocopy would copy.</summary>
        public long Bytes { get; set; }

        /// <summary>False if robocopy reported an error or the pass was cancelled.</summary>
        public bool Complete { get; set; }

        public bool Cancelled { get; set; }
        public string Error { get; set; }
        public int ExitCode { get; set; }
    }

    /// <summary>
    /// Works out how much a run has to copy, so the progress bar has a total.
    ///
    /// It runs robocopy itself with /L, which means "list only": robocopy decides exactly what it
    /// would copy, using the same rules as the real run, and writes the list without copying a
    /// byte. Asking robocopy beats guessing from file dates, because it already knows how it
    /// treats timestamps, attributes and files that are the same.
    ///
    /// Read only. It never uses /MIR or /PURGE and cannot change either side.
    /// </summary>
    public class RobocopyPlanner
    {
        public static string RobocopyPath
        {
            get { return Path.Combine(Environment.SystemDirectory, "robocopy.exe"); }
        }

        /// <summary>
        /// /E /XJ /COPY:DAT /DCOPY:T match the real script, so the same files are selected. /L
        /// makes it list only. /BYTES gives exact sizes. /NDL /NJH /NJS /NP leave only the file
        /// lines. /R:0 /W:0 make an unreachable share fail at once instead of retrying.
        /// </summary>
        public static string BuildArguments(string source, string destination)
        {
            if (string.IsNullOrEmpty(source)) throw new ArgumentException("A source is required.", "source");
            if (string.IsNullOrEmpty(destination)) throw new ArgumentException("A destination is required.", "destination");
            if (source.IndexOf('"') >= 0 || destination.IndexOf('"') >= 0)
                throw new ArgumentException("A path must not contain a quote.");

            return Quote(source) + " " + Quote(destination) +
                   " /E /XJ /L /BYTES /NP /NDL /NJH /NJS /R:0 /W:0 /COPY:DAT /DCOPY:T";
        }

        /// <summary>
        /// A trailing backslash would turn the closing quote into an escaped quote on the command
        /// line, so it is dropped.
        /// </summary>
        private static string Quote(string path)
        {
            string trimmed = path.TrimEnd('\\');
            if (trimmed.Length == 0) trimmed = path;
            return "\"" + trimmed + "\"";
        }

        public Task<PlanResult> PlanAsync(string source, string destination, CancellationToken token)
        {
            return Task.Run(new Func<PlanResult>(delegate { return Plan(source, destination, token); }));
        }

        public PlanResult Plan(string source, string destination, CancellationToken token)
        {
            PlanResult result = new PlanResult();

            string exe = RobocopyPath;
            if (!File.Exists(exe))
            {
                result.Error = "robocopy.exe was not found at " + exe + ".";
                return result;
            }

            string arguments = BuildArguments(source, destination);
            ScriptGenerator.GuardAgainstDeletingOptions("robocopy " + arguments);

            ProcessStartInfo info = new ProcessStartInfo(exe, arguments);
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.StandardOutputEncoding = ScriptGenerator.BatchEncoding;

            string firstError = null;

            try
            {
                using (Process process = Process.Start(info))
                using (token.Register(new Action(delegate { Kill(process); })))
                {
                    string line;
                    while ((line = process.StandardOutput.ReadLine()) != null)
                    {
                        ParsedLogLine parsed = RobocopyLogParser.Parse(line);

                        if (parsed.Kind == LogLineKind.File)
                        {
                            result.Files++;
                            result.Bytes += parsed.Size;
                        }
                        else if (parsed.Kind == LogLineKind.Error && firstError == null)
                        {
                            firstError = StripTimestamp(parsed.Name);
                        }
                    }

                    process.WaitForExit();
                    result.ExitCode = process.ExitCode;
                }
            }
            catch (Exception ex)
            {
                result.Error = "robocopy could not be started: " + ex.Message;
                return result;
            }

            if (token.IsCancellationRequested)
            {
                result.Cancelled = true;
                result.Error = "cancelled";
                return result;
            }

            // 0-7 are success codes (7 means files were copied and extras exist), 8 or more is a failure.
            if (result.ExitCode >= 8)
            {
                result.Error = "robocopy could not read the folders from this PC (exit code " +
                               result.ExitCode + ")" +
                               (firstError == null ? "." : ": " + firstError);
                return result;
            }

            result.Complete = true;
            return result;
        }

        private static void Kill(Process process)
        {
            try
            {
                if (!process.HasExited) process.Kill();
            }
            catch (Exception)
            {
            }
        }

        private static string StripTimestamp(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            return line.Length > 20 ? line.Substring(20).Trim() : line.Trim();
        }
    }
}
