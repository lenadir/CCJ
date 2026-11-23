using System;
using CCJ.Models;
using NUnit.Framework;

namespace CCJ.Tests
{
    /// <summary>
    /// The rules for a list of jobs the user can add to and take from: fresh keys, and jobs that
    /// have been added but not filled in being skipped rather than refused.
    /// </summary>
    [TestFixture]
    public class JobsFileTests
    {
        [Test]
        public void The_first_three_jobs_are_still_the_starting_point()
        {
            JobsFile file = JobsFile.CreateDefault();

            Assert.That(file.Jobs.Count, Is.EqualTo(3));
            Assert.That(file.Jobs[0].Key, Is.EqualTo("Folder1"));
            Assert.That(file.Jobs[2].Key, Is.EqualTo("Folder3"));
        }

        [Test]
        public void A_new_job_takes_the_next_free_key()
        {
            JobsFile file = JobsFile.CreateDefault();

            Assert.That(file.NextFreeKey(), Is.EqualTo("Folder4"));
            Assert.That(file.AddJob().Key, Is.EqualTo("Folder4"));
            Assert.That(file.AddJob().Key, Is.EqualTo("Folder5"));
        }

        [Test]
        public void A_gap_left_by_a_removed_job_is_filled_again()
        {
            JobsFile file = JobsFile.CreateDefault();
            file.Jobs.RemoveAt(1); // Folder2 goes

            Assert.That(file.NextFreeKey(), Is.EqualTo("Folder2"));
        }

        [Test]
        public void A_new_job_starts_empty_and_switched_off()
        {
            JobConfig added = JobsFile.CreateDefault().AddJob();

            Assert.That(added.IsBlank, Is.True);
            Assert.That(added.Enabled, Is.False);
            Assert.That(added.SourcePath, Is.Empty);
            Assert.That(added.NasFolder, Is.Empty);
            Assert.That(added.FirstRunDate.DayOfWeek, Is.EqualTo(DayOfWeek.Sunday));
        }

        [Test]
        public void A_new_job_borrows_the_nas_root_of_the_first_job()
        {
            JobsFile file = JobsFile.CreateDefault();
            file.Jobs[0].NasRoot = @"\\nas\other share";

            Assert.That(file.AddJob().NasRoot, Is.EqualTo(@"\\nas\other share"),
                "the NAS is nearly always the same one, so it is offered ready filled");
        }

        [Test]
        public void A_job_counts_as_filled_in_once_it_has_a_source_or_a_folder()
        {
            JobConfig job = new JobConfig();
            Assert.That(job.IsBlank, Is.True, "a job with nothing in it");

            job.JobName = "Named but empty";
            Assert.That(job.IsBlank, Is.True, "a name alone is not a filled in job");

            job.NasRoot = @"\\nas\share";
            Assert.That(job.IsBlank, Is.True, "the NAS root is offered ready filled, so it proves nothing");

            job.NasFolder = "  ";
            Assert.That(job.IsBlank, Is.True, "spaces are not a folder name");

            job.NasFolder = "Something";
            Assert.That(job.IsBlank, Is.False);

            JobConfig withSourceOnly = new JobConfig();
            withSourceOnly.SourcePath = @"\\host\share";
            Assert.That(withSourceOnly.IsBlank, Is.False);
        }

        [Test]
        public void Saving_drops_the_jobs_that_were_never_filled_in()
        {
            JobsFile file = JobsFile.CreateDefault();
            file.Jobs.RemoveAt(2);
            file.Jobs.RemoveAt(1);
            file.AddJob();
            file.AddJob();

            Assert.That(file.Jobs.Count, Is.EqualTo(3));
            Assert.That(file.ConfiguredJobs.Count, Is.EqualTo(1));

            JobsFile toSave = file.WithoutBlankJobs();

            Assert.That(toSave.Jobs.Count, Is.EqualTo(1), "one filled in job is enough");
            Assert.That(toSave.Jobs[0].Key, Is.EqualTo("Folder1"));
            Assert.That(toSave.VoxRoot, Is.EqualTo(file.VoxRoot));
            Assert.That(file.Jobs.Count, Is.EqualTo(3), "the editors keep their empty jobs");
        }

        [Test]
        public void Dropping_blank_jobs_copies_rather_than_shares_them()
        {
            JobsFile file = JobsFile.CreateDefault();
            JobsFile toSave = file.WithoutBlankJobs();

            toSave.Jobs[0].NasFolder = "changed";

            Assert.That(file.Jobs[0].NasFolder, Is.Not.EqualTo("changed"));
        }
    }
}
