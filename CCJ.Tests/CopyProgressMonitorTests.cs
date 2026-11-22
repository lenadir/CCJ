using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CCJ.Models;
using CCJ.Services;
using NUnit.Framework;

namespace CCJ.Tests
{
    /// <summary>
    /// Drives the monitor with log files laid out exactly as the script and robocopy write them,
    /// so every state a run can be in is checked without touching a network share.
    /// </summary>
    [TestFixture]
    public class CopyProgressMonitorTests
    {
        private const string Folder = "NADIR";

        private string _logs;
        private string _master;
        private DateTime _t0;
        private HashSet<string> _claims;

        [SetUp]
        public void SetUp()
        {
            _logs = ProgressTestSupport.NewTempDir("CCJ_MonitorTests_");
            _master = Path.Combine(_logs, "master.log");
            _t0 = DateTime.Now;
            _claims = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        [TearDown]
        public void TearDown()
        {
            ProgressTestSupport.DeleteQuietly(_logs);
        }

        // ------------------------------------------------------------- helpers

        private TrackedRun Run()
        {
            TrackedRun run = new TrackedRun();
            run.Job = ProgressTestSupport.Job("Folder1", Folder);
            run.LogsDir = _logs;
            run.StartedAt = _t0;
            run.MasterBaseline = 0;
            return run;
        }

        private CopyProgressMonitor Monitor(TrackedRun run)
        {
            return new CopyProgressMonitor(run, _claims);
        }

        private string NewLog(string stamp)
        {
            string path = Path.Combine(_logs, ProgressTestSupport.LogName(Folder, stamp));
            File.WriteAllText(path, ProgressTestSupport.Header("Wed Nov 18 2026 8:00:" + stamp.Substring(stamp.Length - 2)));
            return path;
        }

        private void Master(params string[] lines)
        {
            ProgressTestSupport.Append(_master, string.Join("", lines));
        }

        private static Task<PlanResult> Plan(long files, long bytes)
        {
            PlanResult result = new PlanResult();
            result.Files = files;
            result.Bytes = bytes;
            result.Complete = true;
            return Task.FromResult(result);
        }

        // ------------------------------------------------------------- waiting

        [Test]
        public void Before_the_script_has_written_anything_the_run_is_starting()
        {
            CopyProgress p = Monitor(Run()).Poll(_t0.AddSeconds(2));

            Assert.That(p.Phase, Is.EqualTo(CopyPhase.Waiting));
            Assert.That(p.Message, Does.Contain("Starting"));
            Assert.That(p.BarText, Does.Contain("Starting"));
        }

        [Test]
        public void While_waiting_it_shows_the_attempt_and_why()
        {
            Master(ProgressTestSupport.Started(Folder), ProgressTestSupport.Attempt(2, 6),
                   ProgressTestSupport.Problem("NAS share not reachable: \\\\nas\\share"));

            CopyProgress p = Monitor(Run()).Poll(_t0.AddSeconds(5));

            Assert.That(p.Phase, Is.EqualTo(CopyPhase.Waiting));
            Assert.That(p.Attempt, Is.EqualTo(2));
            Assert.That(p.MaxAttempts, Is.EqualTo(6));
            Assert.That(p.Message, Does.Contain("NAS share not reachable"));
            Assert.That(p.BarText, Does.Contain("attempt 2/6"));
        }

        // ------------------------------------------------------------- copying

        [Test]
        public void Finished_files_are_counted_and_the_newest_line_is_the_one_in_flight()
        {
            Master(ProgressTestSupport.Started(Folder), ProgressTestSupport.Attempt(1, 6));
            string log = NewLog("2026-11-18_08-00-01");
            ProgressTestSupport.Append(log,
                ProgressTestSupport.DirLine("C:\\src\\", 3) +
                ProgressTestSupport.FileLine("a.bin", 100) +
                ProgressTestSupport.FileLine("b.bin", 200) +
                ProgressTestSupport.FileLine("c.bin", 300));

            CopyProgress p = Monitor(Run()).Poll(_t0.AddSeconds(5));

            Assert.That(p.Phase, Is.EqualTo(CopyPhase.Copying));
            Assert.That(p.FilesDone, Is.EqualTo(2), "a and b are finished; c has only just started");
            Assert.That(p.BytesDone, Is.EqualTo(300));
            Assert.That(p.CurrentFile, Is.EqualTo("c.bin"));
            Assert.That(p.CurrentFileBytes, Is.EqualTo(300));
        }

        [Test]
        public void A_new_file_line_finishes_the_previous_file()
        {
            Master(ProgressTestSupport.Started(Folder));
            string log = NewLog("2026-11-18_08-00-01");
            ProgressTestSupport.Append(log, ProgressTestSupport.FileLine("a.bin", 100));
            CopyProgressMonitor monitor = Monitor(Run());

            CopyProgress first = monitor.Poll(_t0.AddSeconds(5));
            Assert.That(first.FilesDone, Is.EqualTo(0));
            Assert.That(first.CurrentFile, Is.EqualTo("a.bin"));

            ProgressTestSupport.Append(log, ProgressTestSupport.FileLine("b.bin", 250));
            CopyProgress second = monitor.Poll(_t0.AddSeconds(7));

            Assert.That(second.FilesDone, Is.EqualTo(1));
            Assert.That(second.BytesDone, Is.EqualTo(100));
            Assert.That(second.CurrentFile, Is.EqualTo("b.bin"));
        }

        [Test]
        public void The_time_on_the_current_file_grows_until_the_next_file_starts()
        {
            Master(ProgressTestSupport.Started(Folder));
            string log = NewLog("2026-11-18_08-00-01");
            ProgressTestSupport.Append(log, ProgressTestSupport.FileLine("big.bin", 6000000000L));
            CopyProgressMonitor monitor = Monitor(Run());

            monitor.Poll(_t0.AddSeconds(5));
            CopyProgress later = monitor.Poll(_t0.AddSeconds(125));

            Assert.That(later.CurrentFileElapsed.TotalSeconds, Is.EqualTo(120).Within(0.5));
            Assert.That(later.CurrentFileBytes, Is.EqualTo(6000000000L));
        }

        [Test]
        public void Extra_files_and_directories_are_not_counted_as_copies()
        {
            Master(ProgressTestSupport.Started(Folder));
            string log = NewLog("2026-11-18_08-00-01");
            ProgressTestSupport.Append(log,
                ProgressTestSupport.DirLine("C:\\src\\", 2) +
                ProgressTestSupport.ExtraLine("only-on-nas.txt", 7) +
                ProgressTestSupport.FileLine("a.bin", 100) +
                ProgressTestSupport.DirLine("C:\\src\\sub\\", 1) +
                ProgressTestSupport.FileLine("b.bin", 200));

            CopyProgress p = Monitor(Run()).Poll(_t0.AddSeconds(5));

            Assert.That(p.FilesDone, Is.EqualTo(1), "only a.bin is finished");
            Assert.That(p.CurrentFile, Is.EqualTo("b.bin"));
        }

        [Test]
        public void Error_lines_are_counted_so_a_struggling_run_is_visible()
        {
            Master(ProgressTestSupport.Started(Folder));
            string log = NewLog("2026-11-18_08-00-01");
            ProgressTestSupport.Append(log,
                ProgressTestSupport.FileLine("locked.bin", 100) +
                ProgressTestSupport.ErrorLine(32, "Copying File C:\\src\\locked.bin") +
                "The process cannot access the file because it is being used by another process.\r\n" +
                ProgressTestSupport.ErrorLine(32, "Copying File C:\\src\\locked.bin"));

            CopyProgress p = Monitor(Run()).Poll(_t0.AddSeconds(5));

            Assert.That(p.ErrorLines, Is.EqualTo(2));
            Assert.That(p.Detail, Does.Contain("2 error line"));
        }

        [Test]
        public void Abbreviated_sizes_are_added_up_close_to_the_real_total()
        {
            Master(ProgressTestSupport.Started(Folder));
            string log = NewLog("2026-11-18_08-00-01");
            // 3,500,000 and 1,200,000 bytes, as robocopy abbreviates them, then a last file in flight.
            ProgressTestSupport.Append(log,
                ProgressTestSupport.FileLine("mega.bin", "3.3 m") +
                ProgressTestSupport.FileLine("wav.wav", "1.1 m") +
                ProgressTestSupport.FileLine("last.bin", 10));

            CopyProgress p = Monitor(Run()).Poll(_t0.AddSeconds(5));

            Assert.That(p.BytesDone, Is.InRange(4600000L, 4800000L));
        }

        // ------------------------------------------------------------- the total and the percentage

        [Test]
        public void With_a_total_the_bar_shows_a_percentage()
        {
            Master(ProgressTestSupport.Started(Folder));
            string log = NewLog("2026-11-18_08-00-01");
            ProgressTestSupport.Append(log,
                ProgressTestSupport.FileLine("a.bin", 100) + ProgressTestSupport.FileLine("b.bin", 100) +
                ProgressTestSupport.FileLine("c.bin", 100) + ProgressTestSupport.FileLine("d.bin", 100) +
                ProgressTestSupport.FileLine("e.bin", 100) + ProgressTestSupport.FileLine("f.bin", 100));

            TrackedRun run = Run();
            run.PrePlan = Plan(10, 1000);

            CopyProgress p = Monitor(run).Poll(_t0.AddSeconds(5));

            Assert.That(p.TotalFiles, Is.EqualTo(10));
            Assert.That(p.TotalBytes, Is.EqualTo(1000));
            Assert.That(p.FilesDone, Is.EqualTo(5));
            Assert.That(p.Fraction.Value, Is.EqualTo(0.5).Within(0.0001));
            Assert.That(p.Percent, Is.EqualTo(50));
            Assert.That(p.BarText, Does.Contain("50%"));
            Assert.That(p.BarText, Does.Contain("/10"));
        }

        [Test]
        public void Bytes_drive_the_percentage_when_the_total_size_is_known()
        {
            Master(ProgressTestSupport.Started(Folder));
            string log = NewLog("2026-11-18_08-00-01");
            // Many tiny files and one huge one: by file count this is 4 of 5 (80%), by bytes it is nearly nothing.
            ProgressTestSupport.Append(log,
                ProgressTestSupport.FileLine("t1", 10) + ProgressTestSupport.FileLine("t2", 10) +
                ProgressTestSupport.FileLine("t3", 10) + ProgressTestSupport.FileLine("t4", 10) +
                ProgressTestSupport.FileLine("huge.bin", 1000000));

            TrackedRun run = Run();
            run.PrePlan = Plan(5, 1000040);

            CopyProgress p = Monitor(run).Poll(_t0.AddSeconds(5));

            Assert.That(p.Fraction.Value, Is.LessThan(0.01), "40 of about a million bytes");
        }

        [Test]
        public void The_bar_never_shows_100_percent_while_the_run_is_still_going()
        {
            Master(ProgressTestSupport.Started(Folder));
            string log = NewLog("2026-11-18_08-00-01");
            // More lines than were planned: the source grew while it was being copied.
            ProgressTestSupport.Append(log,
                ProgressTestSupport.FileLine("a", 100) + ProgressTestSupport.FileLine("b", 100) +
                ProgressTestSupport.FileLine("c", 100) + ProgressTestSupport.FileLine("d", 100));

            TrackedRun run = Run();
            run.PrePlan = Plan(2, 200);

            CopyProgress p = Monitor(run).Poll(_t0.AddSeconds(5));

            Assert.That(p.Phase, Is.EqualTo(CopyPhase.Copying));
            Assert.That(p.Fraction.Value, Is.LessThanOrEqualTo(0.99));
            Assert.That(p.Percent, Is.LessThan(100));
        }

        [Test]
        public void While_the_total_is_being_worked_out_the_bar_has_no_percentage()
        {
            Master(ProgressTestSupport.Started(Folder));
            string log = NewLog("2026-11-18_08-00-01");
            ProgressTestSupport.Append(log, ProgressTestSupport.FileLine("a.bin", 100) + ProgressTestSupport.FileLine("b.bin", 100));

            TrackedRun run = Run();
            run.PrePlan = new TaskCompletionSource<PlanResult>().Task; // never completes

            CopyProgress p = Monitor(run).Poll(_t0.AddSeconds(5));

            Assert.That(p.Planning, Is.True);
            Assert.That(p.Fraction, Is.Null);
            Assert.That(p.TotalFiles, Is.Null);
            Assert.That(p.BarText, Does.Contain("counting"));
            Assert.That(p.FilesDone, Is.EqualTo(1), "the counts still move while the total is pending");
        }

        [Test]
        public void A_failed_plan_leaves_the_total_unknown_and_says_why()
        {
            Master(ProgressTestSupport.Started(Folder));
            string log = NewLog("2026-11-18_08-00-01");
            ProgressTestSupport.Append(log, ProgressTestSupport.FileLine("a.bin", 100));

            PlanResult failed = new PlanResult();
            failed.Complete = false;
            failed.Error = "robocopy could not read the folders from this PC (exit code 16).";

            TrackedRun run = Run();
            run.PrePlan = Task.FromResult(failed);

            CopyProgress p = Monitor(run).Poll(_t0.AddSeconds(5));

            Assert.That(p.TotalFiles, Is.Null, "a failed pass is not 'zero files'");
            Assert.That(p.Fraction, Is.Null);
            Assert.That(p.PlanError, Does.Contain("exit code 16"));
            Assert.That(p.Detail, Does.Contain("Total unknown"));
        }

        [Test]
        public void A_total_of_zero_means_nothing_to_copy_and_the_bar_waits_for_the_result()
        {
            Master(ProgressTestSupport.Started(Folder));
            NewLog("2026-11-18_08-00-01");

            TrackedRun run = Run();
            run.PrePlan = Plan(0, 0);

            CopyProgress p = Monitor(run).Poll(_t0.AddSeconds(5));

            Assert.That(p.TotalFiles, Is.EqualTo(0));
            Assert.That(p.Fraction.Value, Is.LessThan(1.0));
        }

        [Test]
        public void A_planning_pass_is_started_by_the_monitor_once_robocopy_is_running()
        {
            Master(ProgressTestSupport.Started(Folder));
            int started = 0;

            TrackedRun run = Run();
            run.PlanFactory = delegate(CancellationToken token)
            {
                started++;
                return Plan(8, 800);
            };

            CopyProgressMonitor monitor = Monitor(run);

            monitor.Poll(_t0.AddSeconds(2));
            Assert.That(started, Is.EqualTo(0), "no point planning while the shares are still unreachable");

            string log = NewLog("2026-11-18_08-00-01");
            ProgressTestSupport.Append(log, ProgressTestSupport.FileLine("a.bin", 100));
            CopyProgress p = monitor.Poll(_t0.AddSeconds(4));
            p = monitor.Poll(_t0.AddSeconds(6));

            Assert.That(started, Is.EqualTo(1));
            Assert.That(p.TotalFiles, Is.EqualTo(8));
        }

        [Test]
        public void Attaching_part_way_counts_what_was_already_done_on_top_of_what_is_left()
        {
            // The app was opened half way through a run. Files copied so far are not in a new
            // list-only pass any more, so the total is "done so far" + "still to copy".
            Master(ProgressTestSupport.Started(Folder));
            string log = NewLog("2026-11-18_08-00-01");
            ProgressTestSupport.Append(log,
                ProgressTestSupport.FileLine("a", 100) + ProgressTestSupport.FileLine("b", 100) +
                ProgressTestSupport.FileLine("c", 100) + ProgressTestSupport.FileLine("d", 100));

            TrackedRun run = Run();
            run.MasterBaseline = null;
            run.PlanFactory = delegate(CancellationToken token) { return Plan(6, 600); }; // 6 left, counting the one in flight

            CopyProgressMonitor monitor = Monitor(run);
            monitor.Poll(_t0.AddSeconds(5));
            CopyProgress p = monitor.Poll(_t0.AddSeconds(7));

            Assert.That(p.FilesDone, Is.EqualTo(3));
            Assert.That(p.TotalFiles, Is.EqualTo(3 + 6));
            Assert.That(p.TotalBytes, Is.EqualTo(300 + 600));
        }

        // ------------------------------------------------------------- the end of a run

        [Test]
        public void Success_finishes_the_run_and_counts_the_last_file()
        {
            Master(ProgressTestSupport.Started(Folder));
            string log = NewLog("2026-11-18_08-00-01");
            ProgressTestSupport.Append(log, ProgressTestSupport.FileLine("a", 100) + ProgressTestSupport.FileLine("b", 200));
            CopyProgressMonitor monitor = Monitor(Run());
            monitor.Poll(_t0.AddSeconds(5));

            Master(ProgressTestSupport.Success(Folder, log));
            CopyProgress p = monitor.Poll(_t0.AddSeconds(9));

            Assert.That(p.Phase, Is.EqualTo(CopyPhase.Finished));
            Assert.That(monitor.IsComplete, Is.True);
            Assert.That(p.FilesDone, Is.EqualTo(2), "the file that was in flight is finished too");
            Assert.That(p.BytesDone, Is.EqualTo(300));
            Assert.That(p.Fraction.Value, Is.EqualTo(1.0));
            Assert.That(p.CurrentFile, Is.Null);
            Assert.That(p.BarText, Does.Contain("Done"));
        }

        [Test]
        public void A_run_with_nothing_to_copy_finishes_with_zero_files()
        {
            Master(ProgressTestSupport.Started(Folder), ProgressTestSupport.Attempt(1, 6));
            string log = NewLog("2026-11-18_08-00-01"); // robocopy ran and found nothing to do
            Master(ProgressTestSupport.Success(Folder, log));

            CopyProgress p = Monitor(Run()).Poll(_t0.AddSeconds(5));

            Assert.That(p.Phase, Is.EqualTo(CopyPhase.Finished));
            Assert.That(p.FilesDone, Is.EqualTo(0));
            Assert.That(p.BarText, Does.Contain("nothing to copy"));
        }

        [Test]
        public void A_run_that_never_got_as_far_as_robocopy_can_still_finish()
        {
            Master(ProgressTestSupport.Started(Folder), ProgressTestSupport.Success(Folder, Path.Combine(_logs, "gone.log")));

            CopyProgress p = Monitor(Run()).Poll(_t0.AddSeconds(5));

            Assert.That(p.Phase, Is.EqualTo(CopyPhase.Finished));
        }

        [Test]
        public void Failed_after_every_attempt_reports_the_last_problem()
        {
            Master(ProgressTestSupport.Started(Folder), ProgressTestSupport.Attempt(6, 6),
                   ProgressTestSupport.Failed(Folder, "NAS share not reachable: \\\\nas\\share"));

            CopyProgress p = Monitor(Run()).Poll(_t0.AddSeconds(5));

            Assert.That(p.Phase, Is.EqualTo(CopyPhase.Failed));
            Assert.That(p.Message, Does.Contain("NAS share not reachable"));
            Assert.That(p.BarText, Does.Contain("Failed"));
        }

        [Test]
        public void A_finished_monitor_keeps_returning_its_last_result()
        {
            Master(ProgressTestSupport.Started(Folder), ProgressTestSupport.Success(Folder, "x.log"));
            CopyProgressMonitor monitor = Monitor(Run());

            CopyProgress first = monitor.Poll(_t0.AddSeconds(5));
            CopyProgress again = monitor.Poll(_t0.AddSeconds(500));

            Assert.That(again, Is.SameAs(first));
        }

        [Test]
        public void A_task_that_stops_without_a_result_is_reported_as_stopped()
        {
            Master(ProgressTestSupport.Started(Folder));
            TrackedRun run = Run();
            run.IsRunning = delegate { return false; };

            CopyProgress p = Monitor(run).Poll(_t0.AddMinutes(5));

            Assert.That(p.Phase, Is.EqualTo(CopyPhase.Stopped));
            Assert.That(p.Message, Does.Contain("no longer running"));
        }

        [Test]
        public void A_run_is_not_called_stopped_before_it_has_had_time_to_start()
        {
            TrackedRun run = Run();
            run.IsRunning = delegate { return false; }; // Task Scheduler can lag a moment behind

            CopyProgress p = Monitor(run).Poll(_t0.AddSeconds(5));

            Assert.That(p.Phase, Is.EqualTo(CopyPhase.Waiting));
        }

        [Test]
        public void A_run_that_is_still_alive_is_never_called_stopped()
        {
            TrackedRun run = Run();
            run.IsRunning = delegate { return true; };

            CopyProgress p = Monitor(run).Poll(_t0.AddHours(2));

            Assert.That(p.Phase, Is.EqualTo(CopyPhase.Waiting), "waiting for a retry can take up to 15 minutes");
        }

        [Test]
        public void If_the_task_probe_itself_fails_the_run_is_assumed_to_be_alive()
        {
            TrackedRun run = Run();
            run.IsRunning = delegate { throw new InvalidOperationException("Task Scheduler is busy"); };

            CopyProgress p = Monitor(run).Poll(_t0.AddMinutes(5));

            Assert.That(p.Phase, Is.Not.EqualTo(CopyPhase.Stopped));
        }

        [Test]
        public void The_task_is_only_asked_every_ten_seconds()
        {
            int asked = 0;
            TrackedRun run = Run();
            run.IsRunning = delegate { asked++; return true; };
            CopyProgressMonitor monitor = Monitor(run);

            for (int i = 0; i < 8; i++) monitor.Poll(_t0.AddMinutes(1).AddSeconds(i));
            Assert.That(asked, Is.EqualTo(1));

            monitor.Poll(_t0.AddMinutes(1).AddSeconds(12));
            Assert.That(asked, Is.EqualTo(2));
        }

        // ------------------------------------------------------------- which run is whose

        [Test]
        public void An_earlier_runs_result_is_ignored_when_the_start_of_this_run_is_known()
        {
            Master(ProgressTestSupport.Started(Folder), ProgressTestSupport.Success(Folder, "old.log"));
            TrackedRun run = Run();
            run.MasterBaseline = new FileInfo(_master).Length; // the click on Run now

            CopyProgressMonitor monitor = Monitor(run);
            Assert.That(monitor.Poll(_t0.AddSeconds(2)).Phase, Is.EqualTo(CopyPhase.Waiting),
                "last week's SUCCESS must not make a run that has just been started look finished");

            Master(ProgressTestSupport.Started(Folder), ProgressTestSupport.Attempt(1, 6));
            Assert.That(monitor.Poll(_t0.AddSeconds(4)).Phase, Is.EqualTo(CopyPhase.Waiting));
        }

        [Test]
        public void Attaching_to_a_running_job_uses_the_last_job_started_line_and_ignores_older_runs()
        {
            Master(ProgressTestSupport.Started(Folder), ProgressTestSupport.Success(Folder, "week1.log"),
                   ProgressTestSupport.Started(Folder), ProgressTestSupport.Failed(Folder, "week2 problem"),
                   ProgressTestSupport.Started(Folder), ProgressTestSupport.Attempt(1, 6),
                   ProgressTestSupport.Problem("Source folder not reachable: \\\\src\\vox"));
            TrackedRun run = Run();
            run.MasterBaseline = null;

            CopyProgress p = Monitor(run).Poll(_t0.AddSeconds(5));

            Assert.That(p.Phase, Is.EqualTo(CopyPhase.Waiting));
            Assert.That(p.Message, Does.Contain("Source folder not reachable"));
        }

        [Test]
        public void Lines_for_another_folder_name_are_not_mistaken_for_this_run()
        {
            Master(ProgressTestSupport.Started("OTHER"), ProgressTestSupport.Success("OTHER", "other.log"));
            TrackedRun run = Run();
            run.MasterBaseline = null;

            CopyProgress p = Monitor(run).Poll(_t0.AddSeconds(5));

            Assert.That(p.Phase, Is.EqualTo(CopyPhase.Waiting));
        }

        [Test]
        public void A_log_left_by_an_earlier_run_is_not_adopted()
        {
            string old = NewLog("2026-11-10_08-00-01");
            File.SetCreationTime(old, _t0.AddDays(-8));
            Master(ProgressTestSupport.Started(Folder));

            CopyProgressMonitor monitor = Monitor(Run());
            CopyProgress p = monitor.Poll(_t0.AddSeconds(5));

            Assert.That(p.Phase, Is.EqualTo(CopyPhase.Waiting));
            Assert.That(monitor.LogPath, Is.Null);
        }

        [Test]
        public void A_log_from_a_run_that_ended_moments_before_this_one_started_is_not_adopted()
        {
            // Pressing Run now right after a run finished, or running two dry runs back to back:
            // the previous log is only a second old, and it must still not be mistaken for this run's.
            string previous = NewLog("2026-11-18_07-59-58");
            File.SetCreationTime(previous, _t0.AddSeconds(-1));
            Master(ProgressTestSupport.Started(Folder));

            CopyProgressMonitor monitor = Monitor(Run());
            monitor.Poll(_t0.AddSeconds(2));
            Assert.That(monitor.LogPath, Is.Null);

            string current = NewLog("2026-11-18_08-00-03");
            monitor.Poll(_t0.AddSeconds(4));
            Assert.That(monitor.LogPath, Is.EqualTo(current));
        }

        [Test]
        public void The_log_created_by_this_run_is_adopted()
        {
            Master(ProgressTestSupport.Started(Folder));
            string log = NewLog("2026-11-18_08-00-01");

            CopyProgressMonitor monitor = Monitor(Run());
            monitor.Poll(_t0.AddSeconds(5));

            Assert.That(monitor.LogPath, Is.EqualTo(log));
        }

        [Test]
        public void Other_files_in_the_log_folder_are_not_taken_for_a_run_log()
        {
            File.WriteAllText(Path.Combine(_logs, "NADIR_notes.log"), "x");
            File.WriteAllText(Path.Combine(_logs, "source_test.log"), "x");
            File.WriteAllText(Path.Combine(_logs, "NADIR_2_2026-11-18_08-00-01.log"), "x");
            Master(ProgressTestSupport.Started(Folder));

            CopyProgressMonitor monitor = Monitor(Run());
            monitor.Poll(_t0.AddSeconds(5));

            Assert.That(monitor.LogPath, Is.Null);
        }

        [Test]
        public void Two_jobs_with_the_same_folder_name_do_not_read_the_same_log()
        {
            Master(ProgressTestSupport.Started(Folder));
            string first = NewLog("2026-11-18_08-00-01");

            CopyProgressMonitor a = Monitor(Run());
            CopyProgressMonitor b = Monitor(Run());

            a.Poll(_t0.AddSeconds(5));
            b.Poll(_t0.AddSeconds(5));
            Assert.That(a.LogPath, Is.EqualTo(first));
            Assert.That(b.LogPath, Is.Null, "the log is already followed by the first job");

            string second = NewLog("2026-11-18_08-00-02");
            b.Poll(_t0.AddSeconds(7));
            Assert.That(b.LogPath, Is.EqualTo(second));
        }

        [Test]
        public void The_success_line_names_the_log_so_a_run_finished_before_the_app_looked_still_shows_its_totals()
        {
            string log = NewLog("2026-11-18_08-00-01");
            ProgressTestSupport.Append(log,
                ProgressTestSupport.FileLine("a", 100) + ProgressTestSupport.FileLine("b", 200) +
                ProgressTestSupport.FileLine("c", 300));
            Master(ProgressTestSupport.Started(Folder), ProgressTestSupport.Success(Folder, log));

            TrackedRun run = Run();
            run.MasterBaseline = null;
            CopyProgress p = Monitor(run).Poll(_t0.AddSeconds(5));

            Assert.That(p.Phase, Is.EqualTo(CopyPhase.Finished));
            Assert.That(p.FilesDone, Is.EqualTo(3));
            Assert.That(p.BytesDone, Is.EqualTo(600));
        }

        // ------------------------------------------------------------- retries

        [Test]
        public void A_second_attempt_rewrites_the_log_without_the_bar_going_backwards()
        {
            Master(ProgressTestSupport.Started(Folder), ProgressTestSupport.Attempt(1, 6));
            string log = NewLog("2026-11-18_08-00-01");
            ProgressTestSupport.Append(log,
                ProgressTestSupport.FileLine("a", 100) + ProgressTestSupport.FileLine("b", 100) +
                ProgressTestSupport.FileLine("c", 100));

            TrackedRun run = Run();
            run.PrePlan = Plan(10, 1000);
            CopyProgressMonitor monitor = Monitor(run);
            CopyProgress before = monitor.Poll(_t0.AddSeconds(5));
            Assert.That(before.FilesDone, Is.EqualTo(2));

            // Attempt 2: the script runs robocopy again and it overwrites the same log name.
            File.WriteAllText(log, ProgressTestSupport.Header("Wed Nov 18 2026 8:15:09 AM") +
                ProgressTestSupport.FileLine("c", 100));
            Master(ProgressTestSupport.Problem("robocopy failed with exit code 8, see " + log), ProgressTestSupport.Attempt(2, 6));

            CopyProgress after = monitor.Poll(_t0.AddSeconds(900));

            Assert.That(after.FilesDone, Is.GreaterThanOrEqualTo(before.FilesDone), "what attempt 1 finished stays counted");
            Assert.That(after.Fraction.Value, Is.GreaterThanOrEqualTo(before.Fraction.Value));
            Assert.That(after.Attempt, Is.EqualTo(2));
            Assert.That(after.CurrentFile, Is.EqualTo("c"));

            ProgressTestSupport.Append(log, ProgressTestSupport.FileLine("d", 100));
            Assert.That(monitor.Poll(_t0.AddSeconds(902)).FilesDone, Is.EqualTo(3));
        }

        // ------------------------------------------------------------- speed and time left

        [Test]
        public void Speed_and_time_left_appear_after_ten_seconds_of_copying()
        {
            Master(ProgressTestSupport.Started(Folder));
            string log = NewLog("2026-11-18_08-00-01");
            ProgressTestSupport.Append(log, ProgressTestSupport.FileLine("a", 1000000));

            TrackedRun run = Run();
            run.PrePlan = Plan(5, 5000000);
            CopyProgressMonitor monitor = Monitor(run);

            CopyProgress early = monitor.Poll(_t0.AddSeconds(2));
            Assert.That(early.BytesPerSecond, Is.Null, "too early to say");

            ProgressTestSupport.Append(log, ProgressTestSupport.FileLine("b", 1000000) + ProgressTestSupport.FileLine("c", 1000000));
            CopyProgress later = monitor.Poll(_t0.AddSeconds(22));

            Assert.That(later.BytesPerSecond.Value, Is.EqualTo(2000000 / 20.0).Within(1));
            Assert.That(later.Remaining.Value.TotalSeconds, Is.GreaterThan(0));
            Assert.That(later.Detail, Does.Contain("left"));
        }

        [Test]
        public void No_time_left_is_shown_when_the_total_is_unknown()
        {
            Master(ProgressTestSupport.Started(Folder));
            string log = NewLog("2026-11-18_08-00-01");
            ProgressTestSupport.Append(log, ProgressTestSupport.FileLine("a", 1000000));
            CopyProgressMonitor monitor = Monitor(Run());

            monitor.Poll(_t0.AddSeconds(2));
            ProgressTestSupport.Append(log, ProgressTestSupport.FileLine("b", 1000000));
            CopyProgress later = monitor.Poll(_t0.AddSeconds(30));

            Assert.That(later.Remaining, Is.Null);
        }

        // ------------------------------------------------------------- robustness

        [Test]
        public void Missing_logs_folder_is_not_an_error()
        {
            TrackedRun run = Run();
            run.LogsDir = Path.Combine(_logs, "no-such-folder");

            CopyProgress p = Monitor(run).Poll(_t0.AddSeconds(5));

            Assert.That(p.Phase, Is.EqualTo(CopyPhase.Waiting));
        }

        [Test]
        public void A_log_that_disappears_does_not_crash_the_monitor()
        {
            Master(ProgressTestSupport.Started(Folder));
            string log = NewLog("2026-11-18_08-00-01");
            ProgressTestSupport.Append(log, ProgressTestSupport.FileLine("a", 100));
            CopyProgressMonitor monitor = Monitor(run: Run());
            monitor.Poll(_t0.AddSeconds(3));

            File.Delete(log);

            Assert.DoesNotThrow(delegate { monitor.Poll(_t0.AddSeconds(5)); });
        }

        [Test]
        public void A_job_with_no_folder_name_or_no_job_is_refused_up_front()
        {
            Assert.Throws<ArgumentNullException>(delegate { new CopyProgressMonitor(null, _claims); });

            TrackedRun noJob = new TrackedRun();
            Assert.Throws<ArgumentException>(delegate { new CopyProgressMonitor(noJob, _claims); });
        }

        [Test]
        public void Disposing_releases_the_log_so_another_monitor_can_take_it()
        {
            Master(ProgressTestSupport.Started(Folder));
            string log = NewLog("2026-11-18_08-00-01");

            CopyProgressMonitor first = Monitor(Run());
            first.Poll(_t0.AddSeconds(3));
            Assert.That(first.LogPath, Is.EqualTo(log));

            first.Dispose();

            CopyProgressMonitor second = Monitor(Run());
            second.Poll(_t0.AddSeconds(4));
            Assert.That(second.LogPath, Is.EqualTo(log));
        }

        [Test]
        public void Finishing_cancels_a_planning_pass_that_is_still_running()
        {
            Master(ProgressTestSupport.Started(Folder));
            string log = NewLog("2026-11-18_08-00-01");

            CancellationTokenSource source = new CancellationTokenSource();
            TrackedRun run = Run();
            run.PrePlan = new TaskCompletionSource<PlanResult>().Task;
            run.PrePlanSource = source;

            CopyProgressMonitor monitor = Monitor(run);
            monitor.Poll(_t0.AddSeconds(3));
            Assert.That(source.IsCancellationRequested, Is.False);

            Master(ProgressTestSupport.Success(Folder, log));
            monitor.Poll(_t0.AddSeconds(6));

            Assert.That(source.IsCancellationRequested, Is.True, "no point finishing a count for a run that is over");
        }
    }
}
