using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CCJ.Models;
using CCJ.Services;
using NUnit.Framework;

namespace CCJ.Tests
{
    /// <summary>
    /// The whole chain with nothing faked: the real generated script, the real robocopy.exe, the
    /// real log files they write, and the monitor reading them. Every folder is under %TEMP%.
    /// If the progress code and the way robocopy really writes its log ever drift apart, this
    /// is the test that notices.
    /// </summary>
    [TestFixture]
    public class ProgressEndToEndTests
    {
        private const int FileCount = 8;
        private const int FileSize = 400 * 1024;

        private string _root;
        private string _src;
        private string _nas;
        private string _vox;
        private JobConfig _job;

        [SetUp]
        public void SetUp()
        {
            if (!File.Exists(RobocopyPlanner.RobocopyPath))
                Assert.Ignore("robocopy.exe is not at " + RobocopyPlanner.RobocopyPath);

            _root = ProgressTestSupport.NewTempDir("CCJ_EndToEnd_");
            _src = Path.Combine(_root, "source");
            _nas = Path.Combine(_root, "nas");
            _vox = Path.Combine(_root, "vox");

            Directory.CreateDirectory(Path.Combine(_src, "sub"));
            Directory.CreateDirectory(Path.Combine(_src, "sub", "deeper"));
            Directory.CreateDirectory(_nas);
            Directory.CreateDirectory(_vox);

            for (int i = 0; i < FileCount; i++)
            {
                string folder = i % 3 == 0 ? _src : (i % 3 == 1 ? Path.Combine(_src, "sub") : Path.Combine(_src, "sub", "deeper"));
                byte[] data = new byte[FileSize];
                new Random(i).NextBytes(data);
                File.WriteAllBytes(Path.Combine(folder, "recording " + i + (i == 4 ? " été" : "") + ".wav"), data);
            }

            _job = new JobConfig();
            _job.Key = "Folder1";
            _job.JobName = "Folder1";
            _job.SourcePath = _src;
            _job.NasRoot = _nas;
            _job.NasFolder = "Folder1";
            _job.FirstRunDate = JobsFile.DefaultFirstRun();
        }

        [TearDown]
        public void TearDown()
        {
            ProgressTestSupport.DeleteQuietly(_root);
        }

        private static Process Start(string scriptPath)
        {
            ProcessStartInfo info = new ProcessStartInfo("cmd.exe", "/c \"" + scriptPath + "\"");
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            return Process.Start(info);
        }

        /// <summary>Polls like the service does until the run ends, collecting every snapshot.</summary>
        private static List<CopyProgress> Follow(CopyProgressMonitor monitor, Process process)
        {
            List<CopyProgress> seen = new List<CopyProgress>();
            DateTime until = DateTime.Now.AddSeconds(90);

            while (DateTime.Now < until)
            {
                CopyProgress p = monitor.Poll(DateTime.Now);
                seen.Add(p);
                if (monitor.IsComplete) break;
                Thread.Sleep(40);
            }

            if (!process.WaitForExit(30000)) process.Kill();
            return seen;
        }

        [Test]
        public void A_real_copy_is_followed_from_the_first_file_to_SUCCESS()
        {
            ScriptGenerator generator = new ScriptGenerator();
            string scriptPath = _job.ScriptPath(_vox);
            generator.WriteScript(scriptPath, generator.Render(_job, _vox));

            // The confirmation dialog starts this while the user reads it, so the total is ready.
            Task<PlanResult> plan = new RobocopyPlanner().PlanAsync(_src, _job.DestinationPath, CancellationToken.None);
            Assert.That(plan.Result.Complete, Is.True, plan.Result.Error);
            Assert.That(plan.Result.Files, Is.EqualTo(FileCount));

            Process process = null;
            TrackedRun run = new TrackedRun();
            run.Job = _job;
            run.LogsDir = Path.Combine(_vox, "Logs");
            run.StartedAt = DateTime.Now;
            run.MasterBaseline = 0;
            run.PrePlan = plan;
            run.IsRunning = delegate { return process != null && !process.HasExited; };

            CopyProgressMonitor monitor = new CopyProgressMonitor(run, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            process = Start(scriptPath);
            List<CopyProgress> seen = Follow(monitor, process);
            CopyProgress last = seen[seen.Count - 1];

            Assert.That(last.Phase, Is.EqualTo(CopyPhase.Finished), last.Detail);
            Assert.That(monitor.IsComplete, Is.True);

            Assert.That(last.FilesDone, Is.EqualTo(FileCount), "every file robocopy copied was counted");
            Assert.That(last.TotalFiles, Is.EqualTo(FileCount), "the list-only pass predicted exactly that");
            Assert.That(last.BytesDone, Is.EqualTo((long)FileCount * FileSize), "sizes are plain bytes below 10 MB, so exact");
            Assert.That(last.TotalBytes, Is.EqualTo((long)FileCount * FileSize));
            Assert.That(last.Fraction.Value, Is.EqualTo(1.0));
            Assert.That(last.CurrentFile, Is.Null);
            Assert.That(last.LogFile, Does.EndWith(".log"));

            // The destination really holds what the bar claims was copied.
            Assert.That(Directory.GetFiles(_job.DestinationPath, "*.wav", SearchOption.AllDirectories).Length, Is.EqualTo(FileCount));
        }

        [Test]
        public void The_percentage_only_ever_goes_forwards_during_a_real_copy()
        {
            ScriptGenerator generator = new ScriptGenerator();
            string scriptPath = _job.ScriptPath(_vox);
            generator.WriteScript(scriptPath, generator.Render(_job, _vox));

            Task<PlanResult> plan = new RobocopyPlanner().PlanAsync(_src, _job.DestinationPath, CancellationToken.None);
            plan.Wait();

            Process process = null;
            TrackedRun run = new TrackedRun();
            run.Job = _job;
            run.LogsDir = Path.Combine(_vox, "Logs");
            run.StartedAt = DateTime.Now;
            run.MasterBaseline = 0;
            run.PrePlan = plan;
            run.IsRunning = delegate { return process != null && !process.HasExited; };

            CopyProgressMonitor monitor = new CopyProgressMonitor(run, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            process = Start(scriptPath);
            List<CopyProgress> seen = Follow(monitor, process);

            double previous = 0;
            long previousFiles = 0;
            foreach (CopyProgress p in seen)
            {
                Assert.That(p.FilesDone, Is.GreaterThanOrEqualTo(previousFiles), "files done never goes down");
                previousFiles = p.FilesDone;

                if (!p.Fraction.HasValue) continue;
                Assert.That(p.Fraction.Value, Is.GreaterThanOrEqualTo(previous), "the bar never goes backwards");
                previous = p.Fraction.Value;

                if (p.Phase != CopyPhase.Finished)
                    Assert.That(p.Fraction.Value, Is.LessThan(1.0), "100% only once the script has reported SUCCESS");
            }
        }

        [Test]
        public void A_second_run_with_nothing_new_finishes_with_nothing_to_copy()
        {
            ScriptGenerator generator = new ScriptGenerator();
            string scriptPath = _job.ScriptPath(_vox);
            generator.WriteScript(scriptPath, generator.Render(_job, _vox));

            // First run copies everything.
            Process first = Start(scriptPath);
            Assert.That(first.WaitForExit(60000), Is.True);

            // Second run: the destination is already up to date.
            Task<PlanResult> plan = new RobocopyPlanner().PlanAsync(_src, _job.DestinationPath, CancellationToken.None);
            Assert.That(plan.Result.Files, Is.EqualTo(0), "the list-only pass sees nothing left to do");

            Process process = null;
            TrackedRun run = new TrackedRun();
            run.Job = _job;
            run.LogsDir = Path.Combine(_vox, "Logs");
            run.StartedAt = DateTime.Now;
            run.MasterBaseline = new FileInfo(Path.Combine(_vox, "Logs", "master.log")).Length;
            run.PrePlan = plan;
            run.IsRunning = delegate { return process != null && !process.HasExited; };

            CopyProgressMonitor monitor = new CopyProgressMonitor(run, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            process = Start(scriptPath);
            List<CopyProgress> seen = Follow(monitor, process);
            CopyProgress last = seen[seen.Count - 1];

            Assert.That(last.Phase, Is.EqualTo(CopyPhase.Finished), last.Detail);
            Assert.That(last.FilesDone, Is.EqualTo(0));
            Assert.That(last.BarText, Does.Contain("nothing to copy"));
        }

        [Test]
        public void A_dry_run_is_followed_too_and_copies_nothing()
        {
            string dryDir = Path.Combine(_root, "dryrun");
            ScriptGenerator generator = new ScriptGenerator();
            string scriptPath = Path.Combine(dryDir, "Backup_Folder1_DRYRUN.bat");
            generator.WriteScript(scriptPath, generator.RenderDryRun(_job, dryDir));

            Process process = null;
            TrackedRun run = new TrackedRun();
            run.Job = _job;
            run.LogsDir = dryDir;
            run.StartedAt = DateTime.Now;
            run.MasterBaseline = 0;
            run.IsDryRun = true;
            run.PlanFactory = delegate(CancellationToken token)
            {
                return new RobocopyPlanner().PlanAsync(_src, _job.DestinationPath, token);
            };
            run.IsRunning = delegate { return process != null && !process.HasExited; };

            CopyProgressMonitor monitor = new CopyProgressMonitor(run, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            process = Start(scriptPath);
            List<CopyProgress> seen = Follow(monitor, process);
            CopyProgress last = seen[seen.Count - 1];

            Assert.That(last.Phase, Is.EqualTo(CopyPhase.Finished), last.Detail);
            Assert.That(last.IsDryRun, Is.True);
            Assert.That(last.FilesDone, Is.EqualTo(FileCount), "robocopy listed every file it would have copied");
            Assert.That(last.BarText, Does.StartWith("Dry run"));

            Assert.That(Directory.Exists(_job.DestinationPath), Is.False, "a dry run never creates the NAS folder");
            Assert.That(Directory.GetFiles(_nas, "*", SearchOption.AllDirectories), Is.Empty, "and never copies a file");
        }

        [Test]
        public void A_run_that_cannot_reach_the_nas_waits_and_says_why()
        {
            // Point the script at a NAS root that does not exist, and shorten the 15 minute wait so
            // the test can see the retry loop. Only the SETTINGS values change.
            JobConfig unreachable = _job.Clone();
            unreachable.NasRoot = Path.Combine(_root, "no-such-nas");

            ScriptGenerator generator = new ScriptGenerator();
            string script = generator.Render(unreachable, _vox)
                .Replace("set \"WAIT_SECONDS=900\"", "set \"WAIT_SECONDS=3\"")
                .Replace("set \"MAX_ATTEMPTS=6\"", "set \"MAX_ATTEMPTS=2\"");
            string scriptPath = unreachable.ScriptPath(_vox);
            generator.WriteScript(scriptPath, script);

            Process process = null;
            TrackedRun run = new TrackedRun();
            run.Job = unreachable;
            run.LogsDir = Path.Combine(_vox, "Logs");
            run.StartedAt = DateTime.Now;
            run.MasterBaseline = 0;
            run.IsRunning = delegate { return process != null && !process.HasExited; };

            CopyProgressMonitor monitor = new CopyProgressMonitor(run, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            process = Start(scriptPath);

            bool sawWaiting = false;
            string message = null;
            List<CopyProgress> seen = Follow(monitor, process);
            foreach (CopyProgress p in seen)
            {
                if (p.Phase == CopyPhase.Waiting && p.Attempt.HasValue && !string.IsNullOrEmpty(p.Message) &&
                    p.Message.Contains("not reachable"))
                {
                    sawWaiting = true;
                    message = p.Message;
                }
            }

            CopyProgress last = seen[seen.Count - 1];
            Assert.That(sawWaiting, Is.True, "while retrying it should say the NAS cannot be reached");
            Assert.That(message, Does.Contain("NAS share not reachable"));
            Assert.That(last.Phase, Is.EqualTo(CopyPhase.Failed), last.Detail);
            Assert.That(last.Message, Does.Contain("NAS share not reachable"));
            Assert.That(last.FilesDone, Is.EqualTo(0));
        }
    }
}
