using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CCJ.Models;
using CCJ.Services;
using NUnit.Framework;

namespace CCJ.Tests
{
    /// <summary>
    /// Round trip of jobs.json. Every test uses its own file under %TEMP%, never the real
    /// %APPDATA%\CCJ\jobs.json.
    /// </summary>
    [TestFixture]
    public class JobRepositoryTests
    {
        private string _temp;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _temp = Path.Combine(Path.GetTempPath(), "CCJ_RepoTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temp);
            _path = Path.Combine(_temp, "jobs.json");
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_temp)) Directory.Delete(_temp, true);
            }
            catch (Exception)
            {
            }
        }

        private JobsFile GoodFile()
        {
            JobsFile file = JobsFile.CreateDefault();
            file.VoxRoot = _temp;
            return file;
        }

        [Test]
        public void Loading_with_no_file_gives_the_three_default_jobs()
        {
            JobsFile file = new JobRepository(_path).Load();

            Assert.That(file.Jobs.Count, Is.EqualTo(3));
            Assert.That(file.Jobs[0].Key, Is.EqualTo("Folder1"));
            Assert.That(file.Jobs[1].Key, Is.EqualTo("Folder2"));
            Assert.That(file.Jobs[2].Key, Is.EqualTo("Folder3"));
            Assert.That(file.Jobs[0].SourcePath, Is.EqualTo(JobsFile.DefaultSource));
            Assert.That(file.Jobs[0].NasRoot, Is.EqualTo(JobsFile.DefaultNasRoot));
        }

        [Test]
        public void Jobs_are_disabled_by_default_so_nothing_runs_unasked()
        {
            JobsFile file = new JobRepository(_path).Load();

            foreach (JobConfig job in file.Jobs)
                Assert.That(job.Enabled, Is.False);
        }

        [Test]
        public void Save_then_load_returns_the_same_values()
        {
            JobRepository repository = new JobRepository(_path);
            JobsFile saved = GoodFile();
            saved.Jobs[0].JobName = "Recordings";
            saved.Jobs[0].NasFolder = "ENREGISTREMENT NV";
            saved.Jobs[0].Enabled = true;
            saved.Jobs[2].FirstRunDate = new DateTime(2026, 11, 22);

            repository.Save(saved);
            JobsFile loaded = repository.Load();

            Assert.That(File.Exists(_path), Is.True);
            Assert.That(loaded.VoxRoot, Is.EqualTo(_temp));
            Assert.That(loaded.Jobs[0].JobName, Is.EqualTo("Recordings"));
            Assert.That(loaded.Jobs[0].NasFolder, Is.EqualTo("ENREGISTREMENT NV"));
            Assert.That(loaded.Jobs[0].Enabled, Is.True);
            Assert.That(loaded.Jobs[2].FirstRunDate, Is.EqualTo(new DateTime(2026, 11, 22)));
        }

        [Test]
        public void An_invalid_file_is_refused_and_nothing_is_written()
        {
            JobRepository repository = new JobRepository(_path);
            JobsFile file = GoodFile();
            file.Jobs[1].NasFolder = "bad:name";

            ValidationException error = Assert.Throws<ValidationException>(delegate
            {
                repository.Save(file);
            });

            Assert.That(error.Errors.Count, Is.EqualTo(1));
            Assert.That(error.Errors[0], Does.StartWith("Folder2:"));
            Assert.That(File.Exists(_path), Is.False, "an invalid save must not create the file");
        }

        [Test]
        public void A_failed_save_leaves_the_previous_file_untouched()
        {
            JobRepository repository = new JobRepository(_path);
            repository.Save(GoodFile());
            string before = File.ReadAllText(_path);

            JobsFile broken = GoodFile();
            broken.Jobs[0].SourcePath = "not-a-unc-path";
            Assert.Throws<ValidationException>(delegate { repository.Save(broken); });

            Assert.That(File.ReadAllText(_path), Is.EqualTo(before));
        }

        [Test]
        public void Unreadable_json_is_moved_aside_and_defaults_are_returned()
        {
            File.WriteAllText(_path, "{ this is not json", Encoding.UTF8);

            JobsFile file = new JobRepository(_path).Load();

            Assert.That(file.Jobs.Count, Is.EqualTo(3));
            Assert.That(File.Exists(_path + ".bad"), Is.True, "the unreadable file should be kept");
        }

        [Test]
        public void A_file_with_one_job_loads_exactly_that_job()
        {
            File.WriteAllText(_path,
                "{\"Version\":1,\"VoxRoot\":\"" + _temp.Replace("\\", "\\\\") + "\"," +
                "\"Jobs\":[{\"Key\":\"Folder2\",\"JobName\":\"Only one\"," +
                "\"SourcePath\":\"\\\\\\\\h\\\\s\",\"NasRoot\":\"\\\\\\\\h\\\\s\"," +
                "\"NasFolder\":\"F2\",\"FirstRunDate\":\"2026-11-22T00:00:00\",\"Enabled\":true}]}",
                Encoding.UTF8);

            JobsFile file = new JobRepository(_path).Load();

            Assert.That(file.Jobs.Count, Is.EqualTo(1),
                "how many jobs there are is the user's choice, not the program's");
            Assert.That(file.Jobs[0].Key, Is.EqualTo("Folder2"));
            Assert.That(file.Jobs[0].JobName, Is.EqualTo("Only one"));
            Assert.That(file.Jobs[0].Enabled, Is.True);
        }

        [Test]
        public void Five_saved_jobs_come_back_as_five()
        {
            JobRepository repository = new JobRepository(_path);
            JobsFile saved = GoodFile();

            for (int i = 0; i < 2; i++)
            {
                JobConfig added = saved.AddJob();
                added.SourcePath = @"\\host\share";
                added.NasRoot = @"\\nas\share";
                added.NasFolder = "Extra" + i;
            }

            repository.Save(saved);
            JobsFile loaded = repository.Load();

            Assert.That(loaded.Jobs.Count, Is.EqualTo(5));
            Assert.That(loaded.Jobs[3].Key, Is.EqualTo("Folder4"));
            Assert.That(loaded.Jobs[4].NasFolder, Is.EqualTo("Extra1"));
        }

        [Test]
        public void A_job_saved_with_a_time_of_day_keeps_it()
        {
            JobRepository repository = new JobRepository(_path);
            JobsFile saved = GoodFile();
            saved.Jobs[0].FirstRunDate = new DateTime(2026, 11, 22, 2, 15, 0);

            repository.Save(saved);

            Assert.That(repository.Load().Jobs[0].FirstRunDate,
                Is.EqualTo(new DateTime(2026, 11, 22, 2, 15, 0)));
        }

        [Test]
        public void A_job_with_no_key_is_given_one_on_load()
        {
            File.WriteAllText(_path,
                "{\"Version\":1,\"VoxRoot\":\"" + _temp.Replace("\\", "\\\\") + "\"," +
                "\"Jobs\":[{\"JobName\":\"No key\",\"SourcePath\":\"\\\\\\\\h\\\\s\"," +
                "\"NasRoot\":\"\\\\\\\\h\\\\s\",\"NasFolder\":\"F\"}," +
                "{\"Key\":\"Folder1\",\"JobName\":\"Dup\",\"SourcePath\":\"\\\\\\\\h\\\\s\"," +
                "\"NasRoot\":\"\\\\\\\\h\\\\s\",\"NasFolder\":\"G\"}]}",
                Encoding.UTF8);

            JobsFile file = new JobRepository(_path).Load();

            Assert.That(file.Jobs.Count, Is.EqualTo(2));
            Assert.That(file.Jobs[0].Key, Is.EqualTo("Folder1"));
            Assert.That(file.Jobs[1].Key, Is.EqualTo("Folder2"),
                "the duplicate key is moved on, so the two jobs cannot share a script");
            Assert.That(file.Jobs[1].JobName, Is.EqualTo("Dup"));
        }

        [Test]
        public void A_file_with_no_vox_root_gets_one_on_load()
        {
            File.WriteAllText(_path, "{\"Version\":1,\"Jobs\":[]}", Encoding.UTF8);

            JobsFile file = new JobRepository(_path).Load();

            Assert.That(file.VoxRoot, Is.Not.Null.And.Not.Empty);
            Assert.That(file.Jobs.Count, Is.EqualTo(3));
        }

        [Test]
        public void The_saved_file_is_readable_json_with_no_byte_order_mark()
        {
            new JobRepository(_path).Save(GoodFile());

            byte[] bytes = File.ReadAllBytes(_path);
            Assert.That(bytes[0], Is.EqualTo((byte)'{'), "no byte order mark, and indented JSON");
            Assert.That(File.ReadAllText(_path), Does.Contain("\"VoxRoot\""));
        }

        [Test]
        public void No_password_field_is_ever_written()
        {
            new JobRepository(_path).Save(GoodFile());
            string json = File.ReadAllText(_path).ToLowerInvariant();

            Assert.That(json, Does.Not.Contain("password"));
            Assert.That(json, Does.Not.Contain("secret"));
            Assert.That(json, Does.Not.Contain("credential"));
        }

        [Test]
        public void Derived_values_are_not_stored_in_the_json()
        {
            new JobRepository(_path).Save(GoodFile());
            string json = File.ReadAllText(_path);

            Assert.That(json, Does.Not.Contain("DestinationPath"));
            Assert.That(json, Does.Not.Contain("ScriptFileName"));
            Assert.That(json, Does.Not.Contain("TaskName"));
        }
    }
}
