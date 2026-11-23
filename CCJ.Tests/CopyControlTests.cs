using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CCJ.Services;
using NUnit.Framework;

namespace CCJ.Tests
{
    /// <summary>
    /// Pausing, resuming and stopping a copy.
    ///
    /// The suspend and resume tests use a cmd.exe of their own that appends to a file in a loop,
    /// never robocopy and never a real share: what is being proved is that CCJ really can freeze
    /// and unfreeze another process, which is how a paused copy works.
    /// </summary>
    [TestFixture]
    public class CopyControlTests
    {
        private const string LogPath = @"C:\Vox\Logs\NADIR_2026-11-22_00-00-05.log";

        private static string RobocopyCommandLine(string logPath)
        {
            return "robocopy  \"\\\\192.168.100.205\\vox\\ENREGISTREMENT NV\" " +
                   "\"\\\\192.168.100.100\\3G eng\\NADIR\" /E /Z /XJ /R:10 /W:30 /COPY:DAT " +
                   "/DCOPY:T /NP /LOG:\"" + logPath + "\"";
        }

        // ------------------------------------------------------------- finding the right process

        [Test]
        public void The_log_file_in_the_command_line_identifies_the_run()
        {
            Assert.That(CopyControl.MatchesJob(RobocopyCommandLine(LogPath), LogPath, @"C:\Vox\Logs", "NADIR"),
                Is.True);
        }

        [Test]
        public void A_run_of_the_same_job_with_another_log_is_not_this_run()
        {
            string other = @"C:\Vox\Logs\NADIR_2026-11-29_00-00-07.log";

            Assert.That(CopyControl.MatchesJob(RobocopyCommandLine(other), LogPath, @"C:\Vox\Logs", "NADIR"),
                Is.False);
        }

        [Test]
        public void A_dry_run_of_the_same_job_is_not_the_real_run()
        {
            string dryRunLog = @"C:\Users\HP\AppData\Local\Temp\CCJ_DryRun\NADIR_2026-11-22_00-00-05.log";

            Assert.That(CopyControl.MatchesJob(RobocopyCommandLine(dryRunLog), null, @"C:\Vox\Logs", "NADIR"),
                Is.False, "the dry run logs somewhere else, which is what tells them apart");
        }

        [Test]
        public void Before_the_log_is_known_the_folder_and_log_directory_are_enough()
        {
            Assert.That(CopyControl.MatchesJob(RobocopyCommandLine(LogPath), null, @"C:\Vox\Logs", "NADIR"),
                Is.True);
        }

        [Test]
        public void Another_job_logging_in_the_same_folder_is_not_matched()
        {
            string otherJob = @"C:\Vox\Logs\ARCHIVE_2026-11-22_00-00-05.log";

            Assert.That(CopyControl.MatchesJob(RobocopyCommandLine(otherJob), null, @"C:\Vox\Logs", "NADIR"),
                Is.False);
        }

        [Test]
        public void A_command_line_is_matched_whatever_the_case_of_the_path()
        {
            Assert.That(CopyControl.MatchesJob(RobocopyCommandLine(LogPath.ToUpperInvariant()),
                LogPath, @"C:\Vox\Logs", "NADIR"), Is.True);
        }

        [Test]
        public void Nothing_is_matched_without_something_to_match_on()
        {
            Assert.That(CopyControl.MatchesJob(RobocopyCommandLine(LogPath), null, null, null), Is.False);
            Assert.That(CopyControl.MatchesJob("", LogPath, null, null), Is.False);
            Assert.That(CopyControl.MatchesJob(null, LogPath, null, null), Is.False);
        }

        [Test]
        public void Looking_for_a_run_that_is_not_there_finds_nothing()
        {
            string nowhere = Path.Combine(Path.GetTempPath(),
                "CCJ_no_such_run_" + Guid.NewGuid().ToString("N") + ".log");

            Assert.That(CopyControl.Find(nowhere, null, null), Is.Empty);
        }

        // ------------------------------------------------------------- nothing to act on

        [Test]
        public void Pausing_nothing_is_reported_rather_than_claimed_as_done()
        {
            ControlResult result = CopyControl.Pause(new List<RunningCopy>());

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Affected, Is.EqualTo(0));
            Assert.That(result.Error, Does.Contain("no robocopy process"));
        }

        [Test]
        public void Resuming_nothing_is_reported_too()
        {
            ControlResult result = CopyControl.Resume(null);

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Does.Contain("resume"));
        }

        [Test]
        public void Stopping_nothing_is_reported_too()
        {
            ControlResult result = CopyControl.Terminate(new List<RunningCopy>());

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Affected, Is.EqualTo(0));
        }

        // ------------------------------------------------------------- really freezing a process

        [Test]
        public void A_process_stops_writing_while_it_is_paused_and_carries_on_afterwards()
        {
            string dir = ProgressTestSupport.NewTempDir("CCJ_PauseTests_");
            string file = Path.Combine(dir, "ticks.txt");
            Process process = null;

            try
            {
                // Appends to the file as fast as it can, so a freeze is easy to see. It is a script
                // rather than a one liner, to keep cmd.exe's quoting out of the way.
                string script = Path.Combine(dir, "tick.bat");
                File.WriteAllText(script,
                    "@echo off\r\n" +
                    ":loop\r\n" +
                    "echo tick>>\"" + file + "\"\r\n" +
                    "goto loop\r\n");

                ProcessStartInfo info = new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"");
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.WorkingDirectory = dir;

                process = Process.Start(info);

                Assert.That(WaitForGrowth(file, 0, 10000), Is.True, "the helper process never started writing");

                List<RunningCopy> copies = new List<RunningCopy>();
                copies.Add(new RunningCopy(process.Id, "cmd.exe"));

                ControlResult paused = CopyControl.Pause(copies);
                Assert.That(paused.Ok, Is.True, paused.Error);
                Assert.That(paused.Affected, Is.EqualTo(1));

                Thread.Sleep(400);
                long frozenAt = Length(file);
                Thread.Sleep(600);

                Assert.That(Length(file), Is.EqualTo(frozenAt), "a paused process must write nothing");

                ControlResult resumed = CopyControl.Resume(copies);
                Assert.That(resumed.Ok, Is.True, resumed.Error);

                Assert.That(WaitForGrowth(file, frozenAt, 10000), Is.True,
                    "a resumed process must carry on where it left off");

                ControlResult stopped = CopyControl.Terminate(copies);
                Assert.That(stopped.Ok, Is.True, stopped.Error);
                Assert.That(process.WaitForExit(10000), Is.True, "Stop must really end the process");
            }
            finally
            {
                KillQuietly(process);
                ProgressTestSupport.DeleteQuietly(dir);
            }
        }

        [Test]
        public void Stopping_a_process_that_has_already_gone_is_not_an_error()
        {
            Process process = Process.Start(NoOp());
            process.WaitForExit(10000);

            List<RunningCopy> copies = new List<RunningCopy>();
            copies.Add(new RunningCopy(process.Id, "cmd.exe"));

            ControlResult result = CopyControl.Terminate(copies);

            Assert.That(result.Ok, Is.True, "the process is gone, which is what was asked for");
        }

        private static ProcessStartInfo NoOp()
        {
            ProcessStartInfo info = new ProcessStartInfo("cmd.exe", "/c exit 0");
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            return info;
        }

        private static bool WaitForGrowth(string file, long startingFrom, int timeoutMs)
        {
            DateTime until = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < until)
            {
                if (Length(file) > startingFrom) return true;
                Thread.Sleep(25);
            }
            return false;
        }

        private static long Length(string file)
        {
            try
            {
                FileInfo info = new FileInfo(file);
                return info.Exists ? info.Length : 0;
            }
            catch (IOException)
            {
                return 0;
            }
        }

        private static void KillQuietly(Process process)
        {
            if (process == null) return;
            try
            {
                if (!process.HasExited) process.Kill();
            }
            catch (Exception)
            {
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
