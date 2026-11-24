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
    /// Renders a script using the reference script's own values and compares it with
    /// Vox\Backup_Folder1.bat line by line. Only the line 4 comment, which names the destination
    /// of the particular job, is allowed to differ. If anyone edits the template and changes the
    /// retry loop, a label or an ERRORLEVEL check, this test fails.
    /// </summary>
    [TestFixture]
    public class ReferenceFidelityTests
    {
        /// <summary>Line numbers (1 based) that are allowed to differ from the reference.</summary>
        private static readonly int[] LinesAllowedToDiffer = new int[] { 4 };

        private string FindReferenceScript()
        {
            // Walk up from the test assembly to find the repository, so this works from
            // bin\Debug, bin\Release or anywhere else.
            string dir = TestContext.CurrentContext.TestDirectory;

            for (int up = 0; up < 6 && dir != null; up++)
            {
                string candidate = Path.Combine(dir, Path.Combine("Vox", "Backup_Folder1.bat"));
                if (File.Exists(candidate)) return candidate;

                DirectoryInfo parent = Directory.GetParent(dir);
                dir = parent == null ? null : parent.FullName;
            }
            return null;
        }

        [Test]
        public void Generated_script_matches_the_reference_except_the_destination_comment()
        {
            string referencePath = FindReferenceScript();
            if (referencePath == null)
                Assert.Ignore("Vox\\Backup_Folder1.bat was not found next to the solution.");

            // Exactly the values the reference script hard codes.
            JobConfig job = new JobConfig();
            job.Key = "Folder1";
            job.JobName = "Folder1";
            job.SourcePath = @"\\192.168.100.205\vox\ENREGISTREMENT NV\JUILLET-AOUT";
            job.NasRoot = @"\\192.168.100.100\3G eng\ENREGISTREMENT NV";
            job.NasFolder = "NADIR";
            job.FirstRunDate = JobsFile.DefaultFirstRun();

            string rendered = new ScriptGenerator().Render(job, @"C:\Vox");

            string[] referenceLines = File.ReadAllText(referencePath)
                .Replace("\r\n", "\n").Split('\n');
            string[] renderedLines = rendered.Replace("\r\n", "\n").Split('\n');

            Assert.That(renderedLines.Length, Is.EqualTo(referenceLines.Length),
                "the generated script must have the same number of lines as the reference");

            List<string> unexpected = new List<string>();
            for (int i = 0; i < referenceLines.Length; i++)
            {
                if (referenceLines[i] == renderedLines[i]) continue;
                if (IsAllowed(i + 1)) continue;

                unexpected.Add("line " + (i + 1) +
                               "\n  reference: " + referenceLines[i] +
                               "\n  generated: " + renderedLines[i]);
            }

            if (unexpected.Count > 0)
            {
                StringBuilder message = new StringBuilder();
                message.AppendLine("The generated script drifted from " + referencePath + ":");
                foreach (string line in unexpected)
                    message.AppendLine(line);
                Assert.Fail(message.ToString());
            }
        }

        [Test]
        public void The_reference_script_itself_uses_crlf()
        {
            string referencePath = FindReferenceScript();
            if (referencePath == null)
                Assert.Ignore("Vox\\Backup_Folder1.bat was not found next to the solution.");

            byte[] bytes = File.ReadAllBytes(referencePath);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == 10 && (i == 0 || bytes[i - 1] != 13))
                    Assert.Fail("the reference script has a bare LF at byte " + i);
            }
        }

        private static bool IsAllowed(int oneBasedLine)
        {
            foreach (int allowed in LinesAllowedToDiffer)
            {
                if (allowed == oneBasedLine) return true;
            }
            return false;
        }
    }
}
