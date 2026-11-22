using System;
using System.IO;
using System.Text;
using CCJ.Models;
using CCJ.Services;
using NUnit.Framework;

namespace CCJ.Tests
{
    [TestFixture]
    public class LogReaderTests
    {
        private string _temp;
        private string _log;
        private LogReader _reader;

        [SetUp]
        public void SetUp()
        {
            _temp = Path.Combine(Path.GetTempPath(), "CCJ_LogTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temp);
            _log = Path.Combine(_temp, "master.log");
            _reader = new LogReader();
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

        /// <summary>Lines in the format the reference script actually writes.</summary>
        private void WriteSampleLog()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("[11/11/2026 14:21:54,56] ===== Folder1 job started: \"\\\\src\\vox\\Folder1\" to \"\\\\nas\\3G eng\\Folder1\"\r\n");
            builder.Append("[11/11/2026 14:21:54,56] Attempt 1 of 6 \r\n");
            builder.Append("[11/11/2026 14:21:54,58] Problem: Source folder not reachable: \\\\src\\vox\\Folder1. Waiting 900 seconds, then retrying.\r\n");
            builder.Append("[11/11/2026 14:29:12,91] ===== Folder2 job started: \"\\\\src\\vox\\Folder2\" to \"\\\\nas\\3G eng\\Folder2\"\r\n");
            builder.Append("[11/11/2026 14:29:12,92] Attempt 1 of 6 \r\n");
            builder.Append("[11/11/2026 14:30:00,00] SUCCESS: Folder2 copied, robocopy exit code 1. Log: C:\\Vox\\Logs\\Folder2_x.log\r\n");
            builder.Append("[11/11/2026 14:33:22,43] ===== Folder3 job started: \"\\\\src\\vox\\Folder3\" to \"\\\\nas\\3G eng\\Folder3\"\r\n");
            builder.Append("[11/11/2026 14:40:00,00] FAILED: Folder3 not copied after 6 attempts. Last problem: NAS share not reachable \r\n");

            File.WriteAllText(_log, builder.ToString(), Encoding.UTF8);
        }

        [Test]
        public void A_missing_file_gives_an_empty_result_rather_than_throwing()
        {
            Assert.That(_reader.ReadLastLines(Path.Combine(_temp, "nope.log"), 200), Is.Empty);
            Assert.That(_reader.ReadLastLines(null, 200), Is.Empty);
            Assert.That(_reader.LastLineFor(Path.Combine(_temp, "nope.log")), Is.EqualTo(""));
        }

        [Test]
        public void All_lines_are_returned_when_there_is_no_filter()
        {
            WriteSampleLog();

            Assert.That(_reader.ReadLastLines(_log, 200).Length, Is.EqualTo(8));
        }

        [Test]
        public void Blank_lines_are_skipped()
        {
            File.WriteAllText(_log, "one\r\n\r\n   \r\ntwo\r\n", Encoding.UTF8);

            Assert.That(_reader.ReadLastLines(_log, 200).Length, Is.EqualTo(2));
        }

        [Test]
        public void Only_the_last_n_lines_are_returned_oldest_first()
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 1; i <= 500; i++)
                builder.Append("line " + i + "\r\n");
            File.WriteAllText(_log, builder.ToString(), Encoding.UTF8);

            string[] lines = _reader.ReadLastLines(_log, 200);

            Assert.That(lines.Length, Is.EqualTo(200));
            Assert.That(lines[0], Is.EqualTo("line 301"));
            Assert.That(lines[199], Is.EqualTo("line 500"));
        }

        [Test]
        public void The_default_line_count_is_two_hundred()
        {
            Assert.That(LogReader.DefaultLineCount, Is.EqualTo(200));
        }

        [Test]
        public void Filtering_keeps_only_the_lines_for_one_job()
        {
            WriteSampleLog();

            string[] lines = _reader.ReadLastLines(_log, 200, "Folder2");

            Assert.That(lines.Length, Is.EqualTo(2));
            foreach (string line in lines)
                Assert.That(line, Does.Contain("Folder2"));
        }

        [Test]
        public void Filtering_is_case_insensitive()
        {
            WriteSampleLog();

            Assert.That(_reader.ReadLastLines(_log, 200, "folder2").Length, Is.EqualTo(2));
        }

        [Test]
        public void An_unknown_job_matches_nothing()
        {
            WriteSampleLog();

            Assert.That(_reader.ReadLastLines(_log, 200, "Folder9"), Is.Empty);
        }

        [Test]
        public void An_empty_filter_term_is_ignored_rather_than_matching_everything()
        {
            WriteSampleLog();

            Assert.That(_reader.ReadLastLines(_log, 200, new string[] { "" }).Length, Is.EqualTo(8));
            Assert.That(_reader.ReadLastLines(_log, 200, new string[] { null }).Length, Is.EqualTo(8));
        }

        [Test]
        public void Several_filter_terms_are_an_or()
        {
            WriteSampleLog();

            // How the dashboard asks: job key, NAS folder name and job name together.
            string[] lines = _reader.ReadLastLines(_log, 200, "Folder1", "Folder3");

            // Two Folder1 lines and two Folder3 lines. The bare "Attempt 1 of 6" lines name no
            // job, so they are correctly left out.
            Assert.That(lines.Length, Is.EqualTo(4));
            foreach (string line in lines)
                Assert.That(line, Does.Contain("Folder1").Or.Contain("Folder3"));
        }

        [Test]
        public void The_last_line_for_a_job_is_the_most_recent_one()
        {
            WriteSampleLog();

            Assert.That(_reader.LastLineFor(_log, "Folder2"), Does.Contain("SUCCESS: Folder2 copied"));
            Assert.That(_reader.LastLineFor(_log, "Folder3"), Does.Contain("FAILED: Folder3 not copied"));
        }

        [Test]
        public void A_log_the_script_is_still_writing_can_be_read()
        {
            WriteSampleLog();

            // The generated script appends with >>, which keeps the file open for writing.
            using (FileStream writer = new FileStream(_log, FileMode.Append, FileAccess.Write,
                       FileShare.ReadWrite))
            {
                byte[] bytes = Encoding.UTF8.GetBytes("[now] SUCCESS: Folder1 copied\r\n");
                writer.Write(bytes, 0, bytes.Length);
                writer.Flush();

                string[] lines = _reader.ReadLastLines(_log, 200);
                Assert.That(lines.Length, Is.EqualTo(9));
            }
        }

        [Test]
        public void Lf_only_logs_are_read_too()
        {
            File.WriteAllText(_log, "one\ntwo\nthree\n", Encoding.UTF8);

            Assert.That(_reader.ReadLastLines(_log, 200).Length, Is.EqualTo(3));
        }

        [Test]
        public void The_master_log_path_comes_from_the_vox_root()
        {
            JobsFile file = JobsFile.CreateDefault();
            file.VoxRoot = @"C:\Vox";

            Assert.That(file.LogsDir, Is.EqualTo(@"C:\Vox\Logs"));
            Assert.That(file.MasterLogPath, Is.EqualTo(@"C:\Vox\Logs\master.log"));
        }
    }
}
