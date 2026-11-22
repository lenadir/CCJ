using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CCJ.Services;
using NUnit.Framework;

namespace CCJ.Tests
{
    /// <summary>
    /// The planner runs the real robocopy.exe in list-only mode. Every test works on folders
    /// under %TEMP% and the ones that matter most check that nothing was written to either side.
    /// </summary>
    [TestFixture]
    public class RobocopyPlannerTests
    {
        private string _dir;
        private string _src;
        private string _dst;
        private RobocopyPlanner _planner;

        [SetUp]
        public void SetUp()
        {
            _dir = ProgressTestSupport.NewTempDir("CCJ_PlannerTests_");
            _src = Path.Combine(_dir, "src");
            _dst = Path.Combine(_dir, "dst");
            Directory.CreateDirectory(Path.Combine(_src, "sub"));
            _planner = new RobocopyPlanner();
        }

        [TearDown]
        public void TearDown()
        {
            ProgressTestSupport.DeleteQuietly(_dir);
        }

        private static void Make(string path, int bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, new byte[bytes]);
        }

        private static List<string> Snapshot(string dir)
        {
            List<string> entries = new List<string>();
            if (!Directory.Exists(dir)) return entries;

            foreach (string path in Directory.GetFileSystemEntries(dir, "*", SearchOption.AllDirectories))
            {
                FileInfo info = new FileInfo(path);
                entries.Add(path + "|" + (info.Attributes & FileAttributes.Directory) + "|" +
                            ((info.Attributes & FileAttributes.Directory) != 0 ? 0 : info.Length) + "|" +
                            info.LastWriteTimeUtc.Ticks);
            }
            entries.Sort();
            return entries;
        }

        private void RequireRobocopy()
        {
            if (!File.Exists(RobocopyPlanner.RobocopyPath))
                Assert.Ignore("robocopy.exe is not at " + RobocopyPlanner.RobocopyPath);
        }

        // ------------------------------------------------------------- the command line

        [Test]
        public void The_command_line_is_list_only_and_exact()
        {
            string args = RobocopyPlanner.BuildArguments(@"\\server\share\src", @"\\nas\root\dst");

            Assert.That(args, Does.Contain("/L"), "list only: it must never copy");
            Assert.That(args, Does.Contain("/BYTES"));
            Assert.That(args, Does.Contain("/E"));
            Assert.That(args, Does.Contain("/XJ"));
            Assert.That(args, Does.Contain("/R:0"));
            Assert.That(args, Does.Contain("/W:0"));
            Assert.That(args, Does.StartWith("\"\\\\server\\share\\src\" \"\\\\nas\\root\\dst\""));
        }

        [Test]
        public void The_command_line_never_deletes_anything()
        {
            string args = RobocopyPlanner.BuildArguments(@"\\a\b", @"\\c\d").ToUpperInvariant();

            Assert.That(args, Does.Not.Contain("/MIR"));
            Assert.That(args, Does.Not.Contain("/PURGE"));
            Assert.That(args, Does.Not.Contain("/MOV"));
        }

        [Test]
        public void A_trailing_backslash_cannot_swallow_the_closing_quote()
        {
            string args = RobocopyPlanner.BuildArguments(@"\\server\share\src\", @"\\nas\root\dst\");

            Assert.That(args, Does.StartWith("\"\\\\server\\share\\src\" \"\\\\nas\\root\\dst\" "));
        }

        [Test]
        public void A_quote_in_a_path_is_refused()
        {
            Assert.Throws<ArgumentException>(delegate { RobocopyPlanner.BuildArguments("a\"b", @"\\c\d"); });
            Assert.Throws<ArgumentException>(delegate { RobocopyPlanner.BuildArguments(@"\\a\b", "c\"d"); });
        }

        [Test]
        public void Empty_paths_are_refused()
        {
            Assert.Throws<ArgumentException>(delegate { RobocopyPlanner.BuildArguments("", @"\\c\d"); });
            Assert.Throws<ArgumentException>(delegate { RobocopyPlanner.BuildArguments(@"\\a\b", null); });
        }

        // ------------------------------------------------------------- real robocopy

        [Test]
        public void A_brand_new_destination_means_everything_is_to_copy()
        {
            RequireRobocopy();
            Make(Path.Combine(_src, "a.bin"), 10);
            Make(Path.Combine(_src, "b.bin"), 2500);
            Make(Path.Combine(_src, "sub", "c.bin"), 70000);

            PlanResult plan = _planner.Plan(_src, _dst, CancellationToken.None);

            Assert.That(plan.Complete, Is.True, plan.Error);
            Assert.That(plan.Files, Is.EqualTo(3));
            Assert.That(plan.Bytes, Is.EqualTo(10 + 2500 + 70000), "/BYTES makes the sizes exact");
        }

        [Test]
        public void The_list_only_pass_creates_nothing_even_when_the_destination_is_missing()
        {
            RequireRobocopy();
            Make(Path.Combine(_src, "a.bin"), 10);

            _planner.Plan(_src, _dst, CancellationToken.None);

            Assert.That(Directory.Exists(_dst), Is.False, "the destination folder must not be created");
        }

        [Test]
        public void Files_already_on_the_destination_are_not_counted()
        {
            RequireRobocopy();
            Make(Path.Combine(_src, "same.bin"), 500);
            Make(Path.Combine(_src, "new.bin"), 700);

            Directory.CreateDirectory(_dst);
            File.Copy(Path.Combine(_src, "same.bin"), Path.Combine(_dst, "same.bin"));
            File.SetLastWriteTimeUtc(Path.Combine(_dst, "same.bin"),
                File.GetLastWriteTimeUtc(Path.Combine(_src, "same.bin")));

            PlanResult plan = _planner.Plan(_src, _dst, CancellationToken.None);

            Assert.That(plan.Complete, Is.True, plan.Error);
            Assert.That(plan.Files, Is.EqualTo(1), "only the new file has to be copied");
            Assert.That(plan.Bytes, Is.EqualTo(700));
        }

        [Test]
        public void A_changed_file_is_counted_again()
        {
            RequireRobocopy();
            Make(Path.Combine(_src, "a.bin"), 500);
            Directory.CreateDirectory(_dst);
            File.WriteAllBytes(Path.Combine(_dst, "a.bin"), new byte[100]); // different size

            PlanResult plan = _planner.Plan(_src, _dst, CancellationToken.None);

            Assert.That(plan.Files, Is.EqualTo(1));
            Assert.That(plan.Bytes, Is.EqualTo(500));
        }

        [Test]
        public void Files_that_exist_only_on_the_destination_are_not_part_of_the_plan()
        {
            RequireRobocopy();
            Make(Path.Combine(_src, "a.bin"), 100);
            Directory.CreateDirectory(_dst);
            File.WriteAllBytes(Path.Combine(_dst, "only-on-nas.bin"), new byte[9999]);

            PlanResult plan = _planner.Plan(_src, _dst, CancellationToken.None);

            Assert.That(plan.Files, Is.EqualTo(1), "an extra is listed by robocopy but never copied or deleted");
            Assert.That(plan.Bytes, Is.EqualTo(100));
        }

        [Test]
        public void An_up_to_date_destination_has_nothing_to_copy()
        {
            RequireRobocopy();
            Make(Path.Combine(_src, "a.bin"), 100);
            Directory.CreateDirectory(_dst);
            File.Copy(Path.Combine(_src, "a.bin"), Path.Combine(_dst, "a.bin"));
            File.SetLastWriteTimeUtc(Path.Combine(_dst, "a.bin"), File.GetLastWriteTimeUtc(Path.Combine(_src, "a.bin")));

            PlanResult plan = _planner.Plan(_src, _dst, CancellationToken.None);

            Assert.That(plan.Complete, Is.True, plan.Error);
            Assert.That(plan.Files, Is.EqualTo(0));
            Assert.That(plan.Bytes, Is.EqualTo(0));
        }

        [Test]
        public void A_file_with_an_accented_name_is_counted()
        {
            RequireRobocopy();
            Make(Path.Combine(_src, "sub", "\u00E9t\u00E9 enregistrement.wav"), 1234);

            PlanResult plan = _planner.Plan(_src, _dst, CancellationToken.None);

            Assert.That(plan.Files, Is.EqualTo(1));
            Assert.That(plan.Bytes, Is.EqualTo(1234));
        }

        [Test]
        public void Planning_never_changes_either_folder()
        {
            RequireRobocopy();
            Make(Path.Combine(_src, "a.bin"), 100);
            Make(Path.Combine(_src, "sub", "b.bin"), 200);
            Directory.CreateDirectory(_dst);
            File.WriteAllBytes(Path.Combine(_dst, "old.bin"), new byte[50]);

            List<string> srcBefore = Snapshot(_src);
            List<string> dstBefore = Snapshot(_dst);

            _planner.Plan(_src, _dst, CancellationToken.None);

            Assert.That(Snapshot(_src), Is.EqualTo(srcBefore), "the source is untouched");
            Assert.That(Snapshot(_dst), Is.EqualTo(dstBefore), "the destination is untouched: nothing copied, nothing deleted");
        }

        [Test]
        public void An_unreadable_source_is_reported_as_an_incomplete_plan_not_as_zero_files()
        {
            RequireRobocopy();

            PlanResult plan = _planner.Plan(Path.Combine(_dir, "does-not-exist"), _dst, CancellationToken.None);

            Assert.That(plan.Complete, Is.False, "a failed pass must not look like 'nothing to copy'");
            Assert.That(plan.ExitCode, Is.GreaterThanOrEqualTo(8));
            Assert.That(plan.Error, Does.Contain("exit code"));
        }

        [Test]
        public void A_cancelled_plan_says_so()
        {
            RequireRobocopy();
            Make(Path.Combine(_src, "a.bin"), 100);

            CancellationTokenSource source = new CancellationTokenSource();
            source.Cancel();

            PlanResult plan = _planner.Plan(_src, _dst, source.Token);

            Assert.That(plan.Cancelled, Is.True);
            Assert.That(plan.Complete, Is.False);
        }

        [Test]
        public void PlanAsync_gives_the_same_answer_off_the_calling_thread()
        {
            RequireRobocopy();
            Make(Path.Combine(_src, "a.bin"), 321);

            PlanResult plan = _planner.PlanAsync(_src, _dst, CancellationToken.None).Result;

            Assert.That(plan.Complete, Is.True, plan.Error);
            Assert.That(plan.Files, Is.EqualTo(1));
            Assert.That(plan.Bytes, Is.EqualTo(321));
        }
    }
}
