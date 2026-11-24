using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CCJ.Models;
using CCJ.Services;
using NUnit.Framework;

namespace CCJ.Tests
{
    [TestFixture]
    public class ProgressServiceTests
    {
        private string _vox;
        private string _logs;
        private JobsFile _settings;
        private List<CopyProgress> _events;
        private Dictionary<string, TaskRunInfo> _tasks;
        private ProgressService _service;

        [SetUp]
        public void SetUp()
        {
            _vox = ProgressTestSupport.NewTempDir("CCJ_ServiceTests_");
            _logs = Path.Combine(_vox, "Logs");
            Directory.CreateDirectory(_logs);

            _settings = JobsFile.CreateDefault();
            _settings.VoxRoot = _vox;
            _settings.Jobs[0].NasFolder = "ALPHA";
            _settings.Jobs[1].NasFolder = "BRAVO";
            _settings.Jobs[2].NasFolder = "CHARLIE";

            _events = new List<CopyProgress>();
            _tasks = new Dictionary<string, TaskRunInfo>();

            // No planner: these tests must never start robocopy.
            _service = new ProgressService(
                delegate { return _settings; },
                delegate(JobConfig job)
                {
                    TaskRunInfo info;
                    return _tasks.TryGetValue(job.Key, out info) ? info : new TaskRunInfo(false, null);
                },
                null);

            _service.Changed += delegate(object sender, ProgressEventArgs e)
            {
                lock (_events) { _events.Add(e.Progress); }
            };
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            ProgressTestSupport.DeleteQuietly(_vox);
        }

        private string MasterPath { get { return Path.Combine(_logs, "master.log"); } }

        private int EventCount
        {
            get { lock (_events) { return _events.Count; } }
        }

        // ------------------------------------------------------------- tracking a run started by the app

        [Test]
        public void Nothing_is_known_about_a_job_that_has_not_been_tracked()
        {
            Assert.That(_service.Latest("Folder1"), Is.Null);
            Assert.That(_service.IsActive("Folder1"), Is.False);
        }

        [Test]
        public void A_tracked_task_run_is_published_when_polled()
        {
            _service.TrackTask(_settings.Jobs[0]);
            _service.PollOnce();

            CopyProgress latest = _service.Latest("Folder1");
            Assert.That(latest, Is.Not.Null);
            Assert.That(latest.Phase, Is.EqualTo(CopyPhase.Waiting));
            Assert.That(_service.IsActive("Folder1"), Is.True);
            Assert.That(EventCount, Is.GreaterThan(0), "the form is told about it");
        }

        [Test]
        public void Tracking_ignores_what_master_log_already_said_before_the_run()
        {
            File.WriteAllText(MasterPath, ProgressTestSupport.Started("ALPHA") + ProgressTestSupport.Success("ALPHA", "old.log"));

            _service.TrackTask(_settings.Jobs[0]);
            _service.PollOnce();

            Assert.That(_service.Latest("Folder1").Phase, Is.Not.EqualTo(CopyPhase.Finished),
                "an earlier SUCCESS must not make a freshly started run look finished");
        }

        [Test]
        public void A_run_is_followed_to_the_end_and_then_goes_quiet()
        {
            _service.TrackTask(_settings.Jobs[0]);
            _service.PollOnce();

            ProgressTestSupport.Append(MasterPath, ProgressTestSupport.Started("ALPHA") +
                ProgressTestSupport.Success("ALPHA", Path.Combine(_logs, "none.log")));
            _service.PollOnce();

            Assert.That(_service.Latest("Folder1").Phase, Is.EqualTo(CopyPhase.Finished));
            Assert.That(_service.IsActive("Folder1"), Is.False);

            int before = EventCount;
            _service.PollOnce();
            Assert.That(EventCount, Is.EqualTo(before), "a finished run is not announced over and over");
        }

        [Test]
        public void Tracking_a_dry_run_marks_it_and_reads_its_own_folder()
        {
            string dryDir = Path.Combine(_vox, "dry");
            Directory.CreateDirectory(dryDir);
            bool running = true;

            _service.TrackProcess(_settings.Jobs[1], dryDir, delegate { return running; });
            File.WriteAllText(Path.Combine(dryDir, "master.log"),
                ProgressTestSupport.Started("BRAVO") + ProgressTestSupport.Attempt(1, 6));
            _service.PollOnce();

            CopyProgress p = _service.Latest("Folder2");
            Assert.That(p.IsDryRun, Is.True);
            Assert.That(p.BarText, Does.StartWith("Dry run"));
            Assert.That(p.Attempt, Is.EqualTo(1));
        }

        [Test]
        public void Tracking_a_job_again_replaces_the_old_run()
        {
            _service.TrackTask(_settings.Jobs[0]);
            _service.PollOnce();
            ProgressTestSupport.Append(MasterPath, ProgressTestSupport.Started("ALPHA") + ProgressTestSupport.Success("ALPHA", "x.log"));
            _service.PollOnce();
            Assert.That(_service.Latest("Folder1").Phase, Is.EqualTo(CopyPhase.Finished));

            _service.TrackTask(_settings.Jobs[0]); // Run now again
            Assert.That(_service.Latest("Folder1"), Is.Null, "the old result is cleared");
            _service.PollOnce();

            Assert.That(_service.Latest("Folder1").Phase, Is.EqualTo(CopyPhase.Waiting));
        }

        [Test]
        public void Untracking_forgets_the_job()
        {
            _service.TrackTask(_settings.Jobs[0]);
            _service.PollOnce();

            _service.Untrack("Folder1");

            Assert.That(_service.Latest("Folder1"), Is.Null);
            Assert.That(_service.IsActive("Folder1"), Is.False);
        }

        [Test]
        public void Jobs_are_followed_independently()
        {
            _service.TrackTask(_settings.Jobs[0]);
            _service.TrackTask(_settings.Jobs[1]);
            ProgressTestSupport.Append(MasterPath, ProgressTestSupport.Started("ALPHA") + ProgressTestSupport.Success("ALPHA", "a.log") +
                ProgressTestSupport.Started("BRAVO") + ProgressTestSupport.Attempt(2, 6));
            _service.PollOnce();

            Assert.That(_service.Latest("Folder1").Phase, Is.EqualTo(CopyPhase.Finished));
            Assert.That(_service.Latest("Folder2").Phase, Is.EqualTo(CopyPhase.Waiting));
            Assert.That(_service.Latest("Folder2").Attempt, Is.EqualTo(2));
        }

        // ------------------------------------------------------------- pausing

        /// <summary>
        /// A copy that the user paused must say so. The service only records the flag; suspending
        /// robocopy itself is CopyControl's job, which CopyControlTests covers.
        /// </summary>
        [Test]
        public void A_paused_job_is_reported_as_paused_while_the_copy_is_under_way()
        {
            StartCopying("ALPHA");

            Assert.That(_service.Latest("Folder1").Phase, Is.EqualTo(CopyPhase.Copying));
            Assert.That(_service.IsPaused("Folder1"), Is.False);

            _service.SetPaused("Folder1", true);
            _service.PollOnce();

            Assert.That(_service.IsPaused("Folder1"), Is.True);
            Assert.That(_service.Latest("Folder1").Phase, Is.EqualTo(CopyPhase.Paused));
            Assert.That(_service.IsActive("Folder1"), Is.True, "a paused run is not a finished run");

            _service.SetPaused("Folder1", false);
            _service.PollOnce();

            Assert.That(_service.Latest("Folder1").Phase, Is.EqualTo(CopyPhase.Copying));
        }

        [Test]
        public void Pausing_one_job_leaves_the_others_copying()
        {
            StartCopying("ALPHA");
            StartCopying("BRAVO");

            _service.SetPaused("Folder1", true);
            _service.PollOnce();

            Assert.That(_service.Latest("Folder1").Phase, Is.EqualTo(CopyPhase.Paused));
            Assert.That(_service.Latest("Folder2").Phase, Is.EqualTo(CopyPhase.Copying));
        }

        [Test]
        public void A_run_that_ends_while_paused_is_not_left_looking_paused()
        {
            StartCopying("ALPHA");
            _service.SetPaused("Folder1", true);
            _service.PollOnce();
            Assert.That(_service.Latest("Folder1").Phase, Is.EqualTo(CopyPhase.Paused));

            ProgressTestSupport.Append(MasterPath,
                ProgressTestSupport.Success("ALPHA", Path.Combine(_logs, "ALPHA_2026-11-18_08-00-00.log")));
            _service.PollOnce();

            Assert.That(_service.Latest("Folder1").Phase, Is.EqualTo(CopyPhase.Finished));
            Assert.That(_service.IsPaused("Folder1"), Is.False, "the flag must not linger after the run");
        }

        [Test]
        public void The_log_of_the_run_is_available_so_its_robocopy_can_be_found()
        {
            string logName = StartCopying("CHARLIE");

            Assert.That(_service.ActiveLogPath("Folder3"), Is.EqualTo(Path.Combine(_logs, logName)));
            Assert.That(_service.ActiveLogsDir("Folder3"), Is.EqualTo(_logs));
            Assert.That(_service.ActiveFolder("Folder3"), Is.EqualTo("CHARLIE"));
            Assert.That(_service.ActiveIsDryRun("Folder3"), Is.False);
        }

        /// <summary>
        /// Brings one job to the point where robocopy is copying: a tracked run, a "job started"
        /// line and a robocopy log with one file in it.
        /// </summary>
        private string StartCopying(string folder)
        {
            JobConfig job = null;
            foreach (JobConfig candidate in _settings.Jobs)
            {
                if (candidate.NasFolder == folder) job = candidate;
            }

            _service.TrackTask(job);

            string logName = ProgressTestSupport.LogName(folder, "2026-11-18_08-00-00");
            File.WriteAllText(Path.Combine(_logs, logName),
                ProgressTestSupport.Header("Wed Nov 18 2026 8:00:00 AM") +
                ProgressTestSupport.FileLine("take 1.wav", 2048));

            ProgressTestSupport.Append(MasterPath,
                ProgressTestSupport.Started(folder) + ProgressTestSupport.Attempt(1, 6));

            _service.PollOnce();
            return logName;
        }

        // ------------------------------------------------------------- finding a run that is already going

        [Test]
        public void A_task_that_is_already_running_is_found_without_being_asked()
        {
            // The scheduled run started while CCJ was closed.
            DateTime started = DateTime.Now.AddMinutes(-40);
            _tasks["Folder2"] = new TaskRunInfo(true, started);
            File.WriteAllText(MasterPath, ProgressTestSupport.Started("BRAVO") + ProgressTestSupport.Attempt(1, 6) +
                ProgressTestSupport.Problem("Source folder not reachable: \\\\src\\vox"));

            _service.PollOnce();

            Assert.That(_service.IsActive("Folder2"), Is.True);
            Assert.That(_service.Latest("Folder2").Phase, Is.EqualTo(CopyPhase.Waiting));
            Assert.That(_service.Latest("Folder2").Message, Does.Contain("Source folder not reachable"));
            Assert.That(_service.Latest("Folder1"), Is.Null, "jobs whose tasks are idle are left alone");
            Assert.That(_service.Latest("Folder3"), Is.Null);
        }

        [Test]
        public void A_found_run_counts_from_when_the_task_started()
        {
            DateTime started = DateTime.Now.AddMinutes(-40);
            _tasks["Folder3"] = new TaskRunInfo(true, started);
            File.WriteAllText(MasterPath, ProgressTestSupport.Started("CHARLIE"));

            _service.PollOnce();

            Assert.That(_service.Latest("Folder3").Elapsed.TotalMinutes, Is.GreaterThan(39).And.LessThan(41));
        }

        [Test]
        public void A_run_that_just_finished_is_not_picked_up_again_while_Task_Scheduler_still_says_running()
        {
            _service.TrackTask(_settings.Jobs[0]);
            _service.PollOnce();
            ProgressTestSupport.Append(MasterPath, ProgressTestSupport.Started("ALPHA") + ProgressTestSupport.Success("ALPHA", "x.log"));
            _service.PollOnce();
            Assert.That(_service.Latest("Folder1").Phase, Is.EqualTo(CopyPhase.Finished));

            _tasks["Folder1"] = new TaskRunInfo(true, DateTime.Now); // Task Scheduler is a moment behind
            for (int i = 0; i < 12; i++) _service.PollOnce(); // enough polls to include a discovery pass

            Assert.That(_service.Latest("Folder1").Phase, Is.EqualTo(CopyPhase.Finished));
            Assert.That(_service.IsActive("Folder1"), Is.False);
        }

        [Test]
        public void A_failing_Task_Scheduler_does_not_stop_the_other_jobs_being_followed()
        {
            ProgressService failing = new ProgressService(
                delegate { return _settings; },
                delegate(JobConfig job) { throw new InvalidOperationException("Task Scheduler is unavailable"); },
                null);
            try
            {
                failing.TrackTask(_settings.Jobs[0]);
                Assert.DoesNotThrow(delegate { failing.PollOnce(); });
                Assert.That(failing.Latest("Folder1"), Is.Not.Null);
            }
            finally
            {
                failing.Dispose();
            }
        }

        [Test]
        public void DiscoverNow_looks_for_running_tasks_straight_away()
        {
            _tasks["Folder1"] = new TaskRunInfo(true, DateTime.Now.AddMinutes(-1));
            File.WriteAllText(MasterPath, ProgressTestSupport.Started("ALPHA"));
            _service.PollOnce(); // the first poll includes a discovery pass, so move past it
            _service.Untrack("Folder1");

            for (int i = 0; i < 3; i++) _service.PollOnce();
            Assert.That(_service.Latest("Folder1"), Is.Null, "between scheduled looks nothing is found");

            _service.DiscoverNow();
            DateTime until = DateTime.Now.AddSeconds(10);
            while (_service.Latest("Folder1") == null && DateTime.Now < until)
            {
                _service.PollOnce(); // consumes the request if the queued poll was skipped as busy
                Thread.Sleep(50);
            }

            Assert.That(_service.Latest("Folder1"), Is.Not.Null);
        }

        // ------------------------------------------------------------- the timer and shutdown

        [Test]
        public void The_timer_polls_on_its_own()
        {
            _service.TrackTask(_settings.Jobs[0]);
            _service.Start(100);

            DateTime until = DateTime.Now.AddSeconds(10);
            while (EventCount == 0 && DateTime.Now < until) Thread.Sleep(50);

            Assert.That(EventCount, Is.GreaterThan(0));
        }

        [Test]
        public void After_disposal_nothing_more_is_published()
        {
            _service.TrackTask(_settings.Jobs[0]);
            _service.Start(50);
            Thread.Sleep(300);

            _service.Dispose();
            int after = EventCount;
            Thread.Sleep(400);

            Assert.That(EventCount, Is.EqualTo(after));
        }

        [Test]
        public void Disposing_twice_is_harmless()
        {
            _service.Dispose();
            Assert.DoesNotThrow(delegate { _service.Dispose(); });
        }

        [Test]
        public void With_planning_switched_off_a_plan_request_answers_straight_away_with_an_unknown_total()
        {
            PlanResult plan = _service.PlanAhead(_settings.Jobs[0]).Result;

            Assert.That(plan.Complete, Is.False);
            Assert.That(plan.Error, Is.Not.Empty);
        }

        [Test]
        public void Cancelling_a_plan_that_was_never_made_is_harmless()
        {
            Assert.DoesNotThrow(delegate { _service.CancelPlan("Folder1"); });
        }
    }
}
