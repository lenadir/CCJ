using System;
using System.IO;
using System.Text;
using CCJ.Models;
using CCJ.Services;
using NUnit.Framework;

namespace CCJ.Tests
{
    /// <summary>
    /// The generated script must behave exactly like the reference Vox\Backup_Folder1.bat.
    /// Everything here works in a temporary folder; no real source or NAS path is used.
    /// </summary>
    [TestFixture]
    public class ScriptGeneratorTests
    {
        private string _temp;
        private ScriptGenerator _generator;

        [SetUp]
        public void SetUp()
        {
            _temp = Path.Combine(Path.GetTempPath(), "CCJ_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temp);
            _generator = new ScriptGenerator();
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

        private JobConfig SampleJob()
        {
            JobConfig job = new JobConfig();
            job.Key = "Folder1";
            job.JobName = "Folder1";
            job.SourcePath = @"\\192.168.100.205\vox\ENREGISTREMENT NV\JUILLET-AOUT";
            job.NasRoot = @"\\192.168.100.100\3G eng\ENREGISTREMENT NV";
            job.NasFolder = "NADIR";
            job.FirstRunDate = JobsFile.NextSunday();
            job.Enabled = true;
            return job;
        }

        // ------------------------------------------------------------- line endings

        [Test]
        public void Render_uses_crlf_only()
        {
            string script = _generator.Render(SampleJob(), @"C:\Vox");

            Assert.That(script, Does.Contain("\r\n"));
            for (int i = 0; i < script.Length; i++)
            {
                if (script[i] == '\n')
                    Assert.That(i > 0 && script[i - 1] == '\r', Is.True, "bare LF at index " + i);
                if (script[i] == '\r')
                    Assert.That(i + 1 < script.Length && script[i + 1] == '\n', Is.True, "bare CR at index " + i);
            }
        }

        [Test]
        public void NormalizeToCrLf_handles_lf_cr_and_mixed_input()
        {
            Assert.That(ScriptGenerator.NormalizeToCrLf("a\nb"), Is.EqualTo("a\r\nb"));
            Assert.That(ScriptGenerator.NormalizeToCrLf("a\rb"), Is.EqualTo("a\r\nb"));
            Assert.That(ScriptGenerator.NormalizeToCrLf("a\r\nb"), Is.EqualTo("a\r\nb"));
            Assert.That(ScriptGenerator.NormalizeToCrLf("a\r\nb\nc\rd"), Is.EqualTo("a\r\nb\r\nc\r\nd"));
        }

        [Test]
        public void Written_file_has_crlf_and_no_byte_order_mark()
        {
            string path = Path.Combine(_temp, "Backup_Folder1.bat");
            _generator.WriteScript(path, _generator.Render(SampleJob(), _temp));

            byte[] bytes = File.ReadAllBytes(path);

            Assert.That(bytes[0], Is.Not.EqualTo((byte)0xEF), "a byte order mark would break @echo off");

            int crlf = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == 13 && i + 1 < bytes.Length && bytes[i + 1] == 10) crlf++;
                if (bytes[i] == 10 && (i == 0 || bytes[i - 1] != 13))
                    Assert.Fail("bare LF in the written file at byte " + i);
            }
            Assert.That(crlf, Is.EqualTo(72), "the reference script has 72 lines");
        }

        // ------------------------------------------------------------- reference fidelity

        [Test]
        public void Robocopy_options_match_the_reference_exactly()
        {
            string script = _generator.Render(SampleJob(), @"C:\Vox");

            Assert.That(script, Does.Contain(
                "robocopy \"%SRC%\" \"%DST%\" /E /Z /XJ /R:10 /W:30 /COPY:DAT /DCOPY:T /NP /LOG:\"%LOGFILE%\""));
        }

        [Test]
        public void Labels_and_errorlevel_checks_are_preserved()
        {
            string script = _generator.Render(SampleJob(), @"C:\Vox");

            Assert.That(script, Does.Contain("\r\n:TRY\r\n"));
            Assert.That(script, Does.Contain("\r\n:RETRY\r\n"));
            Assert.That(script, Does.Contain("\r\n:SUCCESS\r\n"));
            Assert.That(script, Does.Contain("\r\n:FAILED\r\n"));

            Assert.That(script, Does.Contain("set \"RC=%ERRORLEVEL%\""));
            Assert.That(script, Does.Contain("if %RC% LSS 8 goto SUCCESS"));
            Assert.That(script, Does.Contain("if %ATTEMPT% GEQ %MAX_ATTEMPTS% goto FAILED"));
            Assert.That(script, Does.Contain("goto TRY"));
            Assert.That(script, Does.Contain("exit /b 0"));
            Assert.That(script, Does.Contain("exit /b 1"));
        }

        [Test]
        public void Retry_loop_is_six_attempts_900_seconds_apart()
        {
            string script = _generator.Render(SampleJob(), @"C:\Vox");

            Assert.That(script, Does.Contain("set \"MAX_ATTEMPTS=6\""));
            Assert.That(script, Does.Contain("set \"WAIT_SECONDS=900\""));
            Assert.That(script, Does.Contain("ping -n %WAIT_SECONDS% 127.0.0.1 >nul"));
        }

        [Test]
        public void Both_the_nas_root_and_the_source_are_checked_before_copying()
        {
            string script = _generator.Render(SampleJob(), @"C:\Vox");

            Assert.That(script, Does.Contain("if not exist \"%DST_ROOT%\\\""));
            Assert.That(script, Does.Contain("if not exist \"%SRC%\\\""));
        }

        [Test]
        public void Success_and_failure_are_logged_to_master_log()
        {
            string script = _generator.Render(SampleJob(), @"C:\Vox");

            Assert.That(script, Does.Contain("set \"MASTER=%LOGDIR%\\master.log\""));
            Assert.That(script, Does.Contain("SUCCESS: %FOLDER% copied"));
            Assert.That(script, Does.Contain("FAILED: %FOLDER% not copied"));
        }

        [Test]
        public void Settings_block_carries_the_job_values()
        {
            JobConfig job = SampleJob();
            string script = _generator.Render(job, @"D:\SomeRoot");

            Assert.That(script, Does.Contain("set \"SRC_SERVER=192.168.100.205\""));
            Assert.That(script, Does.Contain("set \"SRC_SHARE=vox\""));
            Assert.That(script, Does.Contain("set \"DST_ROOT=" + job.NasRoot + "\""));
            Assert.That(script, Does.Contain("set \"FOLDER=NADIR\""));
            Assert.That(script, Does.Contain("set \"SRC=" + job.SourcePath + "\""));
            Assert.That(script, Does.Contain("set \"LOGDIR=D:\\SomeRoot\\Logs\""));
            Assert.That(script, Does.Contain("rem  Backup_Folder1.bat"));
        }

        [Test]
        public void Each_job_key_names_its_own_script()
        {
            JobConfig job = SampleJob();
            job.Key = "Folder3";

            Assert.That(job.ScriptFileName, Is.EqualTo("Backup_Folder3.bat"));
            Assert.That(_generator.Render(job, @"C:\Vox"), Does.Contain("rem  Backup_Folder3.bat"));
        }

        // ------------------------------------------------------------- never delete

        [Test]
        public void Rendered_script_never_contains_mir_or_purge()
        {
            string script = _generator.Render(SampleJob(), @"C:\Vox");

            string[] lines = script.Split('\n');
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("rem", StringComparison.OrdinalIgnoreCase)) continue;

                Assert.That(trimmed.ToUpperInvariant(), Does.Not.Contain("/MIR"));
                Assert.That(trimmed.ToUpperInvariant(), Does.Not.Contain("/PURGE"));
            }
        }

        [Test]
        public void Guard_rejects_a_script_that_would_delete_files()
        {
            string bad = "@echo off\r\nrobocopy \"a\" \"b\" /E /MIR\r\n";

            Assert.Throws<InvalidOperationException>(delegate
            {
                ScriptGenerator.GuardAgainstDeletingOptions(bad);
            });
        }

        [Test]
        public void Guard_rejects_purge_too()
        {
            string bad = "@echo off\r\nrobocopy \"a\" \"b\" /PURGE\r\n";

            Assert.Throws<InvalidOperationException>(delegate
            {
                ScriptGenerator.GuardAgainstDeletingOptions(bad);
            });
        }

        [Test]
        public void Guard_allows_the_reference_comment_that_mentions_mir_and_purge()
        {
            // The reference header says "Never deletes anything (no /MIR, no /PURGE)." A guard that
            // tripped on that comment would make the real script unwritable.
            string script = _generator.Render(SampleJob(), @"C:\Vox");

            Assert.That(script, Does.Contain("no /MIR, no /PURGE"));
            Assert.DoesNotThrow(delegate { ScriptGenerator.GuardAgainstDeletingOptions(script); });
        }

        [Test]
        public void WriteScript_refuses_content_with_a_deleting_option()
        {
            string path = Path.Combine(_temp, "bad.bat");

            Assert.Throws<InvalidOperationException>(delegate
            {
                _generator.WriteScript(path, "robocopy a b /MIR\r\n");
            });
            Assert.That(File.Exists(path), Is.False, "nothing must be written when the guard trips");
        }

        // ------------------------------------------------------------- dry run

        [Test]
        public void Dry_run_adds_the_list_only_switch()
        {
            string script = _generator.RenderDryRun(SampleJob(), _temp);

            Assert.That(script, Does.Contain("/COPY:DAT /DCOPY:T /NP /L /LOG:\"%LOGFILE%\""));
        }

        [Test]
        public void Dry_run_does_not_create_the_nas_folder()
        {
            string script = _generator.RenderDryRun(SampleJob(), _temp);

            Assert.That(script, Does.Not.Contain("mkdir \"%DST%\""));
            Assert.That(script, Does.Contain("DRY RUN"));
        }

        [Test]
        public void Dry_run_logs_into_the_folder_it_is_given()
        {
            string script = _generator.RenderDryRun(SampleJob(), _temp);

            Assert.That(script, Does.Contain("set \"LOGDIR=" + _temp + "\""));
        }

        [Test]
        public void Real_run_does_create_the_nas_folder()
        {
            string script = _generator.Render(SampleJob(), @"C:\Vox");

            Assert.That(script, Does.Contain("if not exist \"%DST%\\\" mkdir \"%DST%\""));
            Assert.That(script, Does.Not.Contain(" /L /LOG:"));
        }

        // ------------------------------------------------------------- unsafe values

        [Test]
        public void A_quote_in_a_path_is_refused()
        {
            JobConfig job = SampleJob();
            job.NasFolder = "bad\"name";

            Assert.Throws<InvalidOperationException>(delegate { _generator.Render(job, @"C:\Vox"); });
        }

        [Test]
        public void A_percent_sign_in_a_path_is_refused()
        {
            JobConfig job = SampleJob();
            job.SourcePath = @"\\host\share\%TEMP%";

            Assert.Throws<InvalidOperationException>(delegate { _generator.Render(job, @"C:\Vox"); });
        }

        [Test]
        public void An_unrendered_placeholder_is_caught()
        {
            ScriptGenerator generator = new ScriptGenerator(
                "@echo off\r\nset \"FOLDER={{FOLDER}}\"\r\nset \"X={{NOT_A_REAL_TOKEN}}\"\r\n");

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(delegate
            {
                generator.Render(SampleJob(), @"C:\Vox");
            });
            Assert.That(error.Message, Does.Contain("{{NOT_A_REAL_TOKEN}}"));
        }

        // ------------------------------------------------------------- host parsing

        [Test]
        public void Host_and_share_are_taken_from_the_unc_path()
        {
            Assert.That(ScriptGenerator.HostOf(@"\\192.168.100.205\vox\a\b"), Is.EqualTo("192.168.100.205"));
            Assert.That(ScriptGenerator.ShareOf(@"\\192.168.100.205\vox\a\b"), Is.EqualTo("vox"));
            Assert.That(ScriptGenerator.ShareOf(@"\\192.168.100.100\3G eng"), Is.EqualTo("3G eng"));
            Assert.That(ScriptGenerator.HostOf(""), Is.EqualTo(""));
            Assert.That(ScriptGenerator.HostOf(null), Is.EqualTo(""));
        }

        [Test]
        public void A_local_path_reports_no_host_or_share()
        {
            // "C:" is a drive, not a server, so it must not be written into SRC_SERVER.
            Assert.That(ScriptGenerator.HostOf(@"C:\Vox\source"), Is.EqualTo(""));
            Assert.That(ScriptGenerator.ShareOf(@"C:\Vox\source"), Is.EqualTo(""));
        }
    }
}
