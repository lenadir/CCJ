using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CCJ.Models;

namespace CCJ.Services
{
    /// <summary>
    /// "CCJ.exe --selftest" exercises the service layer without opening the window, using fake
    /// folders under %TEMP% only. It proves the shipped exe can load its dependencies and its
    /// embedded template, and that a generated dry run script really copies nothing.
    ///
    /// It never touches a real source or NAS path.
    /// </summary>
    public static class SelfTest
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);

        private const int AttachParentProcess = -1;

        private static readonly StringBuilder Report = new StringBuilder();
        private static int _passed;
        private static int _failed;

        public static int Run()
        {
            AttachConsole(AttachParentProcess);

            string root = Path.Combine(Path.GetTempPath(), "CCJ_SelfTest_" + DateTime.Now.ToString("HHmmss"));
            Say("CCJ self test");
            Say("working folder: " + root);
            Say("");

            try
            {
                Directory.CreateDirectory(root);
                RunChecks(root);
            }
            catch (Exception ex)
            {
                Fail("self test crashed", ex.ToString());
            }

            Say("");
            Say("passed: " + _passed + "   failed: " + _failed);

            string reportPath = Path.Combine(root, "selftest-report.txt");
            try
            {
                File.WriteAllText(reportPath, Report.ToString(), new UTF8Encoding(false));
                Say("report: " + reportPath);
            }
            catch (Exception)
            {
            }

            return _failed == 0 ? 0 : 1;
        }

        private static void RunChecks(string root)
        {
            string source = Path.Combine(root, "source");
            string nasRoot = Path.Combine(root, "nas");
            string voxRoot = Path.Combine(root, "vox");

            Directory.CreateDirectory(source);
            Directory.CreateDirectory(Path.Combine(source, "sub"));
            Directory.CreateDirectory(nasRoot);
            Directory.CreateDirectory(voxRoot);

            File.WriteAllText(Path.Combine(source, "a.txt"), "first file");
            File.WriteAllText(Path.Combine(source, "b.txt"), "second file");
            File.WriteAllText(Path.Combine(source, "sub", "c.txt"), "third file");

            JobConfig job = new JobConfig();
            job.Key = "Folder1";
            job.JobName = "Folder1";
            job.SourcePath = source;
            job.NasRoot = nasRoot;
            job.NasFolder = "Folder1";
            job.FirstRunDate = JobsFile.NextSunday();
            job.Enabled = true;

            ScriptGenerator generator = new ScriptGenerator();

            // 1. The embedded template really is in the exe.
            string template = null;
            try
            {
                template = generator.LoadTemplate();
                Check("embedded template loads", template != null && template.Contains("robocopy"),
                      template == null ? "null" : template.Length + " chars");
            }
            catch (Exception ex)
            {
                Fail("embedded template loads", ex.Message);
            }

            // 2. The real script keeps the reference behaviour.
            string real = generator.Render(job, voxRoot);
            Check("real script has CRLF only", real.Contains("\r\n") && !HasBareLf(real), "");
            Check("real script keeps the robocopy options",
                  real.Contains(ScriptGenerator.RobocopyOptions + " /LOG:"), "");
            Check("real script keeps the labels",
                  real.Contains(":TRY") && real.Contains(":RETRY") &&
                  real.Contains(":SUCCESS") && real.Contains(":FAILED"), "");
            Check("real script keeps the ERRORLEVEL check",
                  real.Contains("set \"RC=%ERRORLEVEL%\"") && real.Contains("if %RC% LSS 8 goto SUCCESS"), "");
            Check("real script creates the destination",
                  real.Contains("if not exist \"%DST%\\\" mkdir \"%DST%\""), "");
            Check("real script has 6 attempts and 900 seconds",
                  real.Contains("set \"MAX_ATTEMPTS=6\"") && real.Contains("set \"WAIT_SECONDS=900\""), "");
            Check("real script has no /MIR or /PURGE outside comments", NoDeletingOptions(real), "");

            // 3. The dry run script cannot copy anything.
            string dryRunDir = Path.Combine(root, "dryrun");
            string dry = generator.RenderDryRun(job, dryRunDir);
            Check("dry run adds /L", dry.Contains("/NP /L /LOG:"), "");
            Check("dry run does not create the destination",
                  !dry.Contains("mkdir \"%DST%\""), "");
            Check("dry run logs under TEMP", dry.Contains("set \"LOGDIR=" + dryRunDir + "\""), "");

            // 4. Writing the files.
            string realPath = job.ScriptPath(voxRoot);
            generator.WriteScript(realPath, real);
            Check("real script written", File.Exists(realPath), realPath);

            string dryPath = Path.Combine(dryRunDir, "Backup_Folder1_DRYRUN.bat");
            generator.WriteScript(dryPath, dry);
            Check("dry run script written", File.Exists(dryPath), dryPath);

            byte[] bytes = File.ReadAllBytes(dryPath);
            Check("written file has no byte order mark",
                  !(bytes.Length > 2 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF), "");

            // 5. Actually run the dry run and prove nothing was copied.
            string destination = Path.Combine(nasRoot, "Folder1");
            int exitCode = RunBatch(dryPath, dryRunDir);
            Check("dry run exit code is 0", exitCode == 0, "exit " + exitCode);
            Check("dry run created no destination folder", !Directory.Exists(destination),
                  destination);

            string master = Path.Combine(dryRunDir, "master.log");
            Check("dry run wrote master.log", File.Exists(master), master);

            if (File.Exists(master))
            {
                string text = File.ReadAllText(master);
                Check("dry run logged SUCCESS", text.Contains("SUCCESS"), FirstLineContaining(text, "SUCCESS"));
            }

            // 6. Validation rules.
            Check("Sunday accepted", Validation.IsSunday(JobsFile.NextSunday()), "");
            Check("Monday rejected", !Validation.IsSunday(JobsFile.NextSunday().AddDays(1)), "");
            Check("good folder name accepted", Validation.IsValidFolderName("Folder1"), "");
            Check("folder name with a backslash rejected", !Validation.IsValidFolderName("a\\b"), "");
            Check("folder name with a colon rejected", !Validation.IsValidFolderName("a:b"), "");
            Check("UNC with a space in the share accepted",
                  Validation.IsValidUncPath(@"\\192.168.100.100\3G eng"), "");
            Check("local path rejected as UNC", !Validation.IsValidUncPath(@"C:\Vox"), "");

            // 6b. Jobs the user adds and leaves empty, and the time of the weekly run.
            JobsFile file = JobsFile.CreateDefault();
            Check("three starter jobs", file.Jobs.Count == 3, file.Jobs.Count + " jobs");

            JobConfig added = file.AddJob();
            Check("a new job gets a free key", added.Key == "Folder4", added.Key);
            Check("a new job starts empty, so it is skipped", added.IsBlank, "");
            Check("an empty job is left out when saving",
                  file.WithoutBlankJobs().Jobs.Count == 3, file.WithoutBlankJobs().Jobs.Count + " kept");

            JobConfig oneOnly = new JobConfig();
            oneOnly.Key = "Folder1";
            oneOnly.JobName = "Folder1";
            oneOnly.SourcePath = @"\\host\share";
            oneOnly.NasRoot = @"\\nas\share";
            oneOnly.NasFolder = "Folder1";
            oneOnly.FirstRunDate = JobsFile.NextSunday().AddHours(2).AddMinutes(30);

            JobsFile single = new JobsFile();
            single.VoxRoot = voxRoot;
            single.Jobs.Add(oneOnly);
            Check("one filled in job is enough to save", Validation.ValidateFile(single).Count == 0, "");
            Check("a run time other than midnight is allowed",
                  Validation.ValidateJob(oneOnly).Count == 0, "02:30 on a Sunday");

            // 6c. Finding the robocopy process of a run, which is what Pause, Resume and Stop act on.
            string runLog = @"C:\Vox\Logs\NADIR_2026-11-22_00-00-05.log";
            string commandLine = "robocopy \"\\\\src\\vox\" \"\\\\nas\\share\\NADIR\" " +
                                 ScriptGenerator.RobocopyOptions + " /LOG:\"" + runLog + "\"";

            Check("the run's robocopy is matched by its log",
                  CopyControl.MatchesJob(commandLine, runLog, @"C:\Vox\Logs", "NADIR"), "");
            Check("another run of the same job is not matched",
                  !CopyControl.MatchesJob(commandLine, @"C:\Vox\Logs\NADIR_2026-11-29_00-00-07.log",
                                          @"C:\Vox\Logs", "NADIR"), "");
            Check("a dry run of the same job is not matched",
                  !CopyControl.MatchesJob(
                      commandLine.Replace(@"C:\Vox\Logs", Path.Combine(Path.GetTempPath(), "CCJ_DryRun")),
                      null, @"C:\Vox\Logs", "NADIR"), "");
            Check("nothing is suspended when nothing is running",
                  !CopyControl.Pause(new System.Collections.Generic.List<RunningCopy>()).Ok, "");

            // 7. The progress bar: follow a real copy made by the real script.
            CheckProgress(root);

            // 8. Log reader.
            LogReader reader = new LogReader();
            string[] lines = reader.ReadLastLines(master, 200);
            Check("log reader returns lines", lines.Length > 0, lines.Length + " lines");

            string[] filtered = reader.ReadLastLines(master, 200, "Folder1");
            Check("log reader filters by job", filtered.Length > 0, filtered.Length + " lines");

            string[] none = reader.ReadLastLines(master, 200, "NoSuchJobName");
            Check("log reader filter excludes other jobs", none.Length == 0, none.Length + " lines");
        }

        /// <summary>
        /// Runs the real generated script on a small temporary tree while the progress monitor
        /// follows it. This is the check that robocopy's log, in this Windows language, is read
        /// the way the progress bar expects.
        /// </summary>
        private static void CheckProgress(string root)
        {
            const int fileCount = 6;
            const int fileSize = 300 * 1024;

            string src = Path.Combine(root, "progress_source");
            string nas = Path.Combine(root, "progress_nas");
            string vox = Path.Combine(root, "progress_vox");
            Directory.CreateDirectory(Path.Combine(src, "sub"));
            Directory.CreateDirectory(nas);
            Directory.CreateDirectory(vox);

            for (int i = 0; i < fileCount; i++)
            {
                string folder = i % 2 == 0 ? src : Path.Combine(src, "sub");
                byte[] data = new byte[fileSize];
                new Random(i).NextBytes(data);
                File.WriteAllBytes(Path.Combine(folder, "take " + i + " été.wav"), data);
            }

            JobConfig job = new JobConfig();
            job.Key = "Folder1";
            job.JobName = "Folder1";
            job.SourcePath = src;
            job.NasRoot = nas;
            job.NasFolder = "Folder1";
            job.FirstRunDate = JobsFile.NextSunday();

            ScriptGenerator generator = new ScriptGenerator();
            string scriptPath = job.ScriptPath(vox);
            generator.WriteScript(scriptPath, generator.Render(job, vox));

            RobocopyPlanner planner = new RobocopyPlanner();
            Task<PlanResult> plan = planner.PlanAsync(src, job.DestinationPath, CancellationToken.None);
            PlanResult planned = plan.Result;
            Check("progress: list-only pass finds the files to copy",
                  planned.Complete && planned.Files == fileCount && planned.Bytes == (long)fileCount * fileSize,
                  planned.Files + " files, " + planned.Bytes + " bytes" + (planned.Error == null ? "" : ", " + planned.Error));
            Check("progress: the list-only pass copied nothing", !Directory.Exists(job.DestinationPath), job.DestinationPath);

            Process process = null;
            TrackedRun run = new TrackedRun();
            run.Job = job;
            run.LogsDir = Path.Combine(vox, "Logs");
            run.StartedAt = DateTime.Now;
            run.MasterBaseline = 0;
            run.PrePlan = plan;
            run.IsRunning = delegate { return process != null && !process.HasExited; };

            CopyProgressMonitor monitor = new CopyProgressMonitor(
                run, new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase));

            ProcessStartInfo info = new ProcessStartInfo("cmd.exe", "/c \"" + scriptPath + "\"");
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            process = Process.Start(info);

            CopyProgress last = null;
            double highest = 0;
            bool wentBackwards = false;
            bool reachedFullEarly = false;
            DateTime until = DateTime.Now.AddSeconds(90);

            while (DateTime.Now < until)
            {
                last = monitor.Poll(DateTime.Now);

                if (last.Fraction.HasValue)
                {
                    if (last.Fraction.Value < highest) wentBackwards = true;
                    highest = last.Fraction.Value;
                    if (last.Phase != CopyPhase.Finished && last.Fraction.Value >= 1.0) reachedFullEarly = true;
                }

                if (monitor.IsComplete) break;
                Thread.Sleep(40);
            }
            process.WaitForExit(30000);

            Check("progress: the run was seen to finish", last != null && last.Phase == CopyPhase.Finished,
                  last == null ? "no snapshot" : last.Phase.ToString());
            Check("progress: every copied file was counted",
                  last != null && last.FilesDone == fileCount && last.TotalFiles == fileCount,
                  last == null ? "" : last.FilesDone + " of " + last.TotalFiles);
            Check("progress: bytes add up exactly",
                  last != null && last.BytesDone == (long)fileCount * fileSize,
                  last == null ? "" : last.BytesDone + " bytes");
            Check("progress: the bar never went backwards", !wentBackwards, "");
            Check("progress: 100% only once the script said SUCCESS", !reachedFullEarly, "");
            Check("progress: the files really are on the destination",
                  Directory.Exists(job.DestinationPath) &&
                  Directory.GetFiles(job.DestinationPath, "*.wav", SearchOption.AllDirectories).Length == fileCount, "");

            PlanResult again = planner.Plan(src, job.DestinationPath, CancellationToken.None);
            Check("progress: afterwards the list-only pass has nothing left to copy",
                  again.Complete && again.Files == 0, again.Files + " files");
        }

        private static int RunBatch(string scriptPath, string workingDirectory)
        {
            ProcessStartInfo info = new ProcessStartInfo("cmd.exe", "/c \"" + scriptPath + "\"");
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.WorkingDirectory = workingDirectory;

            using (Process process = Process.Start(info))
            {
                process.WaitForExit(120000);
                return process.HasExited ? process.ExitCode : -1;
            }
        }

        private static bool HasBareLf(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n' && (i == 0 || text[i - 1] != '\r'))
                    return true;
            }
            return false;
        }

        private static bool NoDeletingOptions(string script)
        {
            try
            {
                ScriptGenerator.GuardAgainstDeletingOptions(script);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string FirstLineContaining(string text, string term)
        {
            string[] lines = text.Split('\n');
            foreach (string line in lines)
            {
                if (line.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    return line.Trim();
            }
            return "";
        }

        private static void Check(string what, bool ok, string detail)
        {
            if (ok)
            {
                _passed++;
                Say("  PASS  " + what + (detail.Length > 0 ? "   (" + detail + ")" : ""));
            }
            else
            {
                Fail(what, detail);
            }
        }

        private static void Fail(string what, string detail)
        {
            _failed++;
            Say("  FAIL  " + what + (string.IsNullOrEmpty(detail) ? "" : "   (" + detail + ")"));
        }

        private static void Say(string line)
        {
            Report.AppendLine(line);
            try
            {
                Console.WriteLine(line);
            }
            catch (Exception)
            {
            }
        }
    }
}
