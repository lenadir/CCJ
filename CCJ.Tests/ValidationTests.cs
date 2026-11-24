using System;
using System.Collections.Generic;
using CCJ.Models;
using CCJ.Services;
using NUnit.Framework;

namespace CCJ.Tests
{
    [TestFixture]
    public class ValidationTests
    {
        // ------------------------------------------------------- when the first run may be
        //
        // There is no weekday rule. A job can be scheduled for any day and any time, including
        // today, and the weekly trigger then repeats on the weekday that was chosen.

        [Test]
        [TestCase(2026, 11, 22)] // Sunday
        [TestCase(2026, 11, 16)] // Monday
        [TestCase(2026, 11, 17)] // Tuesday
        [TestCase(2026, 11, 18)] // Wednesday
        [TestCase(2026, 11, 19)] // Thursday
        [TestCase(2026, 11, 20)] // Friday
        [TestCase(2026, 11, 21)] // Saturday
        public void Any_weekday_can_be_the_first_run(int year, int month, int day)
        {
            JobConfig job = GoodJob();
            job.FirstRunDate = new DateTime(year, month, day);

            Assert.That(Validation.ValidateJob(job), Is.Empty,
                new DateTime(year, month, day).DayOfWeek + " must be allowed");
        }

        [Test]
        public void A_first_run_today_is_accepted()
        {
            JobConfig job = GoodJob();
            job.FirstRunDate = DateTime.Now;

            Assert.That(Validation.ValidateJob(job), Is.Empty,
                "a job must be schedulable straight away, without waiting for a particular day");
        }

        [Test]
        public void A_first_run_in_the_past_is_accepted()
        {
            JobConfig job = GoodJob();
            job.FirstRunDate = new DateTime(2020, 1, 2, 7, 15, 0);

            Assert.That(Validation.ValidateJob(job), Is.Empty,
                "an old start date is Task Scheduler's business, not a validation error");
        }

        [Test]
        public void The_default_first_run_is_a_few_minutes_away_on_a_round_five_minutes()
        {
            DateTime next = JobsFile.DefaultFirstRun();

            Assert.That(next, Is.GreaterThan(DateTime.Now), "it must still be ahead of us");
            Assert.That(next, Is.LessThan(DateTime.Now.AddMinutes(10)), "and only just ahead");
            Assert.That(next.Minute % 5, Is.EqualTo(0));
            Assert.That(next.Second, Is.EqualTo(0));
        }

        [Test]
        public void A_first_run_can_have_a_time_of_day()
        {
            JobConfig job = GoodJob();
            job.FirstRunDate = new DateTime(2026, 11, 23, 3, 30, 0); // Monday, 03:30

            Assert.That(Validation.ValidateJob(job), Is.Empty,
                "the day and the time of the weekly run are both the user's to choose");
        }

        // ------------------------------------------------------------- folder names

        [Test]
        [TestCase("Folder1")]
        [TestCase("NADIR")]
        [TestCase("ENREGISTREMENT NV")]
        [TestCase("2026-11-22")]
        [TestCase("a")]
        [TestCase("Mod_3G  Aout  2026  HARMONY")]
        public void Good_folder_names_are_accepted(string name)
        {
            Assert.That(Validation.IsValidFolderName(name), Is.True, name);
        }

        [Test]
        [TestCase("a\\b")]
        [TestCase("a/b")]
        [TestCase("a:b")]
        [TestCase("a*b")]
        [TestCase("a?b")]
        [TestCase("a\"b")]
        [TestCase("a<b")]
        [TestCase("a>b")]
        [TestCase("a|b")]
        public void Every_character_the_spec_forbids_is_rejected(string name)
        {
            Assert.That(Validation.IsValidFolderName(name), Is.False, name);
        }

        [Test]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("  leading")]
        [TestCase("trailing  ")]
        [TestCase(".")]
        [TestCase("..")]
        [TestCase("name.")]
        [TestCase("CON")]
        [TestCase("con")]
        [TestCase("NUL")]
        [TestCase("COM1")]
        [TestCase("LPT9")]
        [TestCase("PRN.txt")]
        public void Empty_padded_dotted_and_reserved_names_are_rejected(string name)
        {
            Assert.That(Validation.IsValidFolderName(name), Is.False, "[" + name + "]");
        }

        [Test]
        public void A_null_folder_name_is_rejected()
        {
            Assert.That(Validation.IsValidFolderName(null), Is.False);
        }

        [Test]
        public void A_control_character_in_a_folder_name_is_rejected()
        {
            Assert.That(Validation.IsValidFolderName("a\tb"), Is.False);
            Assert.That(Validation.IsValidFolderName("a\rb"), Is.False);
        }

        [Test]
        public void A_very_long_folder_name_is_rejected()
        {
            Assert.That(Validation.IsValidFolderName(new string('a', 500)), Is.False);
        }

        // ------------------------------------------------------------- UNC paths

        [Test]
        [TestCase(@"\\192.168.100.205\vox")]
        [TestCase(@"\\192.168.100.100\3G eng")]
        [TestCase(@"\\192.168.100.100\3G eng\ENREGISTREMENT NV")]
        [TestCase(@"\\192.168.100.205\vox\ENREGISTREMENT NV\JUILLET-AOUT")]
        [TestCase(@"\\nas-01\share")]
        [TestCase(@"\\server.local\share\sub")]
        [TestCase(@"\\192.168.100.205\vox\")]
        public void Good_unc_paths_are_accepted(string path)
        {
            Assert.That(Validation.IsValidUncPath(path), Is.True, path);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(@"C:\Vox")]
        [TestCase(@"\\192.168.100.205")]
        [TestCase(@"\\")]
        [TestCase(@"\\\share")]
        [TestCase(@"//192.168.100.205/vox")]
        [TestCase(@"\\192.168.100.205\vox\bad|name")]
        [TestCase(@"\\192.168.100.205\vox\bad*name")]
        [TestCase(@"\\192.168.100.205\ ")]
        [TestCase(@"vox")]
        public void Bad_unc_paths_are_rejected(string path)
        {
            Assert.That(Validation.IsValidUncPath(path), Is.False, "[" + path + "]");
        }

        // ------------------------------------------------------------- whole job

        [Test]
        public void A_good_job_has_no_errors()
        {
            Assert.That(Validation.ValidateJob(GoodJob()), Is.Empty);
        }

        [Test]
        public void An_empty_job_reports_every_field()
        {
            List<string> errors = Validation.ValidateJob(new JobConfig());

            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(4),
                "job name, source, NAS root and folder should all be reported");
        }

        [Test]
        public void A_job_name_with_a_path_separator_is_rejected()
        {
            JobConfig job = GoodJob();
            job.JobName = @"Folder\1";

            Assert.That(Validation.ValidateJob(job), Is.Not.Empty);
        }

        [Test]
        public void A_null_job_is_reported_rather_than_throwing()
        {
            Assert.That(Validation.ValidateJob(null), Is.Not.Empty);
        }

        [Test]
        public void The_default_file_is_valid_apart_from_nothing()
        {
            JobsFile file = JobsFile.CreateDefault();

            Assert.That(file.Jobs.Count, Is.EqualTo(3));
            Assert.That(Validation.ValidateFile(file), Is.Empty);
        }

        [Test]
        public void Errors_are_prefixed_with_the_job_key()
        {
            JobsFile file = JobsFile.CreateDefault();
            file.Jobs[1].NasFolder = "bad:name";

            List<string> errors = Validation.ValidateFile(file);

            Assert.That(errors.Count, Is.EqualTo(1));
            Assert.That(errors[0], Does.StartWith("Folder2:"));
        }

        [Test]
        public void A_file_with_more_than_three_jobs_is_valid()
        {
            JobsFile file = JobsFile.CreateDefault();
            JobConfig added = file.AddJob();
            added.SourcePath = @"\\host\share";
            added.NasRoot = @"\\nas\share";
            added.NasFolder = "Extra";

            Assert.That(file.Jobs.Count, Is.EqualTo(4));
            Assert.That(added.Key, Is.EqualTo("Folder4"));
            Assert.That(Validation.ValidateFile(file), Is.Empty);
        }

        [Test]
        public void A_file_with_a_single_job_is_valid()
        {
            JobsFile file = JobsFile.CreateDefault();
            file.Jobs.RemoveAt(2);
            file.Jobs.RemoveAt(1);

            Assert.That(file.Jobs.Count, Is.EqualTo(1));
            Assert.That(Validation.ValidateFile(file), Is.Empty, "one job is enough");
        }

        [Test]
        public void A_file_with_no_jobs_at_all_is_reported()
        {
            JobsFile file = JobsFile.CreateDefault();
            file.Jobs.Clear();

            List<string> errors = Validation.ValidateFile(file);

            Assert.That(errors.Count, Is.EqualTo(1));
            Assert.That(errors[0], Does.Contain("at least one job"));
        }

        [Test]
        public void Two_jobs_with_the_same_key_are_reported()
        {
            JobsFile file = JobsFile.CreateDefault();
            file.Jobs[1].Key = "Folder1";
            file.Jobs[1].JobName = "Second";

            List<string> errors = Validation.ValidateFile(file);

            Assert.That(errors.Count, Is.EqualTo(1));
            Assert.That(errors[0], Does.Contain("Backup_Folder1.bat"),
                "two jobs with one key would write the same script");
        }

        [Test]
        public void Two_jobs_with_the_same_name_are_reported()
        {
            JobsFile file = JobsFile.CreateDefault();
            file.Jobs[1].JobName = file.Jobs[0].JobName;

            List<string> errors = Validation.ValidateFile(file);

            Assert.That(errors.Count, Is.EqualTo(1));
            Assert.That(errors[0], Does.StartWith("Folder2:"));
            Assert.That(errors[0], Does.Contain("already used by Folder1"),
                "two jobs with one name would fight over one scheduled task");
        }

        [Test]
        public void An_empty_vox_root_is_reported()
        {
            JobsFile file = JobsFile.CreateDefault();
            file.VoxRoot = "";

            Assert.That(Validation.ValidateFile(file), Is.Not.Empty);
        }

        // ------------------------------------------------------------- derived values

        [Test]
        public void Destination_is_the_nas_root_plus_the_folder()
        {
            JobConfig job = GoodJob();
            job.NasRoot = @"\\192.168.100.100\3G eng";
            job.NasFolder = "Folder1";

            Assert.That(job.DestinationPath, Is.EqualTo(@"\\192.168.100.100\3G eng\Folder1"));
        }

        [Test]
        public void Destination_tolerates_a_trailing_separator_on_the_root()
        {
            JobConfig job = GoodJob();
            job.NasRoot = @"\\192.168.100.100\3G eng\";
            job.NasFolder = "Folder1";

            Assert.That(job.DestinationPath, Is.EqualTo(@"\\192.168.100.100\3G eng\Folder1"));
        }

        [Test]
        public void The_task_name_follows_the_spec()
        {
            JobConfig job = GoodJob();
            job.JobName = "Folder2";

            Assert.That(job.TaskName, Is.EqualTo("CCJ Backup Folder2"));
        }

        private static JobConfig GoodJob()
        {
            JobConfig job = new JobConfig();
            job.Key = "Folder1";
            job.JobName = "Folder1";
            job.SourcePath = @"\\192.168.100.205\vox";
            job.NasRoot = @"\\192.168.100.100\3G eng";
            job.NasFolder = "Folder1";
            job.FirstRunDate = JobsFile.DefaultFirstRun();
            job.Enabled = true;
            return job;
        }
    }
}
