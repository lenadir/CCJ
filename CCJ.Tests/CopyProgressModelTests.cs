using System;
using System.Globalization;
using CCJ.Models;
using NUnit.Framework;

namespace CCJ.Tests
{
    [TestFixture]
    public class CopyProgressModelTests
    {
        private static CopyProgress Copying(long done, long? totalFiles, long bytes, long? totalBytes)
        {
            CopyProgress p = new CopyProgress();
            p.Key = "Folder1";
            p.Phase = CopyPhase.Copying;
            p.FilesDone = done;
            p.TotalFiles = totalFiles;
            p.BytesDone = bytes;
            p.TotalBytes = totalBytes;
            p.Elapsed = TimeSpan.FromMinutes(3);
            return p;
        }

        // ------------------------------------------------------------- the fraction

        [Test]
        public void Bytes_are_preferred_over_files_when_both_totals_are_known()
        {
            CopyProgress p = Copying(900, 1000, 100, 1000);

            Assert.That(p.Fraction.Value, Is.EqualTo(0.1).Within(0.0001));
        }

        [Test]
        public void Files_are_used_when_the_byte_total_is_not_known()
        {
            CopyProgress p = Copying(25, 100, 0, null);

            Assert.That(p.Fraction.Value, Is.EqualTo(0.25).Within(0.0001));
        }

        [Test]
        public void Without_any_total_there_is_no_fraction()
        {
            Assert.That(Copying(25, null, 100, null).Fraction, Is.Null);
        }

        [Test]
        public void A_running_bar_stops_at_99_percent()
        {
            CopyProgress p = Copying(500, 100, 5000, 100);

            Assert.That(p.Fraction.Value, Is.EqualTo(0.99).Within(0.0001));
            Assert.That(p.Percent, Is.EqualTo(99));
        }

        [Test]
        public void A_finished_run_is_always_100_percent()
        {
            CopyProgress p = Copying(3, null, 10, null);
            p.Phase = CopyPhase.Finished;

            Assert.That(p.Fraction.Value, Is.EqualTo(1.0));
            Assert.That(p.IsActive, Is.False);
        }

        [Test]
        public void A_failed_run_does_not_pretend_to_be_complete()
        {
            CopyProgress p = Copying(30, 100, 0, null);
            p.Phase = CopyPhase.Failed;

            Assert.That(p.Fraction.Value, Is.EqualTo(0.3).Within(0.0001));
        }

        [Test]
        public void Active_means_waiting_or_copying()
        {
            CopyProgress p = new CopyProgress();

            p.Phase = CopyPhase.Waiting; Assert.That(p.IsActive, Is.True);
            p.Phase = CopyPhase.Copying; Assert.That(p.IsActive, Is.True);
            p.Phase = CopyPhase.Finished; Assert.That(p.IsActive, Is.False);
            p.Phase = CopyPhase.Failed; Assert.That(p.IsActive, Is.False);
            p.Phase = CopyPhase.Stopped; Assert.That(p.IsActive, Is.False);
        }

        // ------------------------------------------------------------- the bar text

        [Test]
        public void The_bar_text_for_each_phase()
        {
            CopyProgress waiting = new CopyProgress();
            waiting.Phase = CopyPhase.Waiting;
            Assert.That(waiting.BarText, Is.EqualTo("Starting..."));

            waiting.Attempt = 3;
            waiting.MaxAttempts = 6;
            Assert.That(waiting.BarText, Does.Contain("attempt 3/6"));

            CopyProgress copying = Copying(120, 480, 0, null);
            Assert.That(copying.BarText, Does.Contain("25%"));
            Assert.That(copying.BarText, Does.Contain("files"));

            CopyProgress done = new CopyProgress();
            done.Phase = CopyPhase.Finished;
            done.FilesDone = 7;
            Assert.That(done.BarText, Does.StartWith("Done"));

            CopyProgress failed = new CopyProgress();
            failed.Phase = CopyPhase.Failed;
            Assert.That(failed.BarText, Is.EqualTo("Failed"));

            CopyProgress stopped = new CopyProgress();
            stopped.Phase = CopyPhase.Stopped;
            Assert.That(stopped.BarText, Is.EqualTo("Stopped"));
        }

        [Test]
        public void A_dry_run_is_labelled_in_the_bar_and_the_detail()
        {
            CopyProgress p = Copying(1, 10, 0, null);
            p.IsDryRun = true;

            Assert.That(p.BarText, Does.StartWith("Dry run"));
            Assert.That(p.Detail, Does.Contain("dry run"));
        }

        [Test]
        public void With_no_total_yet_the_bar_shows_a_plain_count()
        {
            CopyProgress p = Copying(42, null, 0, null);

            Assert.That(p.BarText, Does.Contain("42"));
            Assert.That(p.BarText, Does.Not.Contain("%"));
        }

        [Test]
        public void ToString_is_the_bar_text_so_the_grid_can_show_it()
        {
            CopyProgress p = Copying(5, 10, 0, null);

            Assert.That(p.ToString(), Is.EqualTo(p.BarText));
        }

        // ------------------------------------------------------------- the detail text

        [Test]
        public void The_detail_names_the_file_in_flight_and_how_long_it_has_been_going()
        {
            CopyProgress p = Copying(10, 20, 5000, 20000);
            p.CurrentFile = "enregistrement-07.wav";
            p.CurrentFileBytes = 3 * 1024 * 1024;
            p.CurrentFileElapsed = TimeSpan.FromSeconds(75);

            Assert.That(p.Detail, Does.Contain("Now copying: enregistrement-07.wav"));
            Assert.That(p.Detail, Does.Contain("3 MB"));
            Assert.That(p.Detail, Does.Contain("1m 15s"));
        }

        [Test]
        public void The_detail_shows_speed_and_time_left_when_known()
        {
            CopyProgress p = Copying(10, 20, 5000, 20000);
            p.BytesPerSecond = 2 * 1024 * 1024;
            p.Remaining = TimeSpan.FromMinutes(12);

            Assert.That(p.Detail, Does.Contain("2 MB/s"));
            Assert.That(p.Detail, Does.Contain("12m 00s left"));
        }

        [Test]
        public void While_waiting_the_detail_shows_the_reason_instead_of_a_file()
        {
            CopyProgress p = new CopyProgress();
            p.Key = "Folder1";
            p.Phase = CopyPhase.Waiting;
            p.Message = "NAS share not reachable";

            Assert.That(p.Detail, Does.Contain("NAS share not reachable"));
            Assert.That(p.Detail, Does.Not.Contain("Now copying"));
        }

        [Test]
        public void The_detail_explains_why_there_is_no_percentage()
        {
            CopyProgress planning = Copying(3, null, 0, null);
            planning.Planning = true;
            Assert.That(planning.Detail, Does.Contain("Counting"));

            CopyProgress unknown = Copying(3, null, 0, null);
            unknown.PlanError = "robocopy could not read the folders from this PC (exit code 16).";
            Assert.That(unknown.Detail, Does.Contain("Total unknown"));
            Assert.That(unknown.Detail, Does.Contain("exit code 16"));
        }

        // ------------------------------------------------------------- formatting

        [Test]
        public void Sizes_are_shown_in_readable_units()
        {
            string decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

            Assert.That(SizeFormat.Bytes(0), Is.EqualTo("0 B"));
            Assert.That(SizeFormat.Bytes(1023), Is.EqualTo("1023 B"));
            Assert.That(SizeFormat.Bytes(1024), Is.EqualTo("1 KB"));
            Assert.That(SizeFormat.Bytes(1536), Is.EqualTo("1" + decimalSeparator + "5 KB"));
            Assert.That(SizeFormat.Bytes(5L * 1024 * 1024), Is.EqualTo("5 MB"));
            Assert.That(SizeFormat.Bytes(250L * 1024 * 1024), Is.EqualTo("250 MB"));
            Assert.That(SizeFormat.Bytes(3L * 1024 * 1024 * 1024), Is.EqualTo("3 GB"));
            Assert.That(SizeFormat.Bytes(-5), Is.EqualTo("0 B"));
        }

        [Test]
        public void Durations_are_shown_in_the_two_largest_units()
        {
            Assert.That(SizeFormat.Duration(TimeSpan.FromSeconds(0)), Is.EqualTo("0s"));
            Assert.That(SizeFormat.Duration(TimeSpan.FromSeconds(45)), Is.EqualTo("45s"));
            Assert.That(SizeFormat.Duration(TimeSpan.FromSeconds(75)), Is.EqualTo("1m 15s"));
            Assert.That(SizeFormat.Duration(TimeSpan.FromMinutes(61)), Is.EqualTo("1h 01m"));
            Assert.That(SizeFormat.Duration(TimeSpan.FromHours(26)), Is.EqualTo("26h 00m"));
            Assert.That(SizeFormat.Duration(TimeSpan.FromSeconds(-5)), Is.EqualTo("0s"));
        }
    }
}
