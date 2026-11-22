using System;
using CCJ.Services;
using NUnit.Framework;

namespace CCJ.Tests
{
    /// <summary>
    /// The samples are lines robocopy really writes. The French ones were captured from this
    /// machine's own robocopy; the English ones follow robocopy's English labels. The parser must
    /// treat both the same, because it works from the shape of a line, never from its labels.
    /// </summary>
    [TestFixture]
    public class RobocopyLogParserTests
    {
        // ------------------------------------------------------------- file lines

        [Test]
        public void A_french_file_line_is_a_file_with_its_exact_size()
        {
            ParsedLogLine line = RobocopyLogParser.Parse("\t  Nouveau fichier\t\t    2500\tkilo.bin");

            Assert.That(line.Kind, Is.EqualTo(LogLineKind.File));
            Assert.That(line.Name, Is.EqualTo("kilo.bin"));
            Assert.That(line.Size, Is.EqualTo(2500));
            Assert.That(line.SizeIsExact, Is.True);
        }

        [Test]
        public void An_english_file_line_is_read_the_same_way()
        {
            ParsedLogLine line = RobocopyLogParser.Parse("\t  New File  \t\t    2500\tkilo.bin");

            Assert.That(line.Kind, Is.EqualTo(LogLineKind.File));
            Assert.That(line.Size, Is.EqualTo(2500));
        }

        [Test]
        [TestCase("\t  Newer      \t\t     123\ta.txt")]
        [TestCase("\t  Older      \t\t     123\ta.txt")]
        [TestCase("\t  Modified   \t\t     123\ta.txt")]
        [TestCase("\t  Plus r\u00E9cent\t\t     123\ta.txt")]
        public void Every_class_of_file_robocopy_copies_is_a_file(string text)
        {
            Assert.That(RobocopyLogParser.Parse(text).Kind, Is.EqualTo(LogLineKind.File), text);
        }

        [Test]
        public void The_number_of_tabs_between_the_class_and_the_size_does_not_matter()
        {
            Assert.That(RobocopyLogParser.Parse("\t  New File\t\t    10\ta.txt").Kind, Is.EqualTo(LogLineKind.File));
            Assert.That(RobocopyLogParser.Parse("\t  A much longer class label\t    10\ta.txt").Kind, Is.EqualTo(LogLineKind.File));
        }

        [Test]
        public void File_names_with_spaces_and_brackets_are_kept_whole()
        {
            ParsedLogLine line = RobocopyLogParser.Parse("\t  New File  \t\t   12345\tMod 3G  Aout (final) [2].wav");

            Assert.That(line.Kind, Is.EqualTo(LogLineKind.File));
            Assert.That(line.Name, Is.EqualTo("Mod 3G  Aout (final) [2].wav"));
        }

        [Test]
        public void A_full_path_from_a_list_only_pass_is_kept_as_the_name()
        {
            ParsedLogLine line = RobocopyLogParser.Parse(
                "\t  Nouveau fichier\t\t    2500\tC:\\Users\\HP\\src\\sub dir\\kilo.bin");

            Assert.That(line.Kind, Is.EqualTo(LogLineKind.File));
            Assert.That(line.Name, Is.EqualTo("C:\\Users\\HP\\src\\sub dir\\kilo.bin"));
        }

        // ------------------------------------------------------------- sizes

        [Test]
        public void A_size_beyond_two_gigabytes_is_exact_when_robocopy_prints_bytes()
        {
            ParsedLogLine line = RobocopyLogParser.Parse("\t  New File  \t\t6000000000\thuge.bin");

            Assert.That(line.Size, Is.EqualTo(6000000000L));
            Assert.That(line.SizeIsExact, Is.True);
        }

        [Test]
        public void An_abbreviated_size_is_converted_using_powers_of_1024()
        {
            // Real output: a 3,500,000 byte file is printed as "3.3 m".
            ParsedLogLine line = RobocopyLogParser.Parse("\t  Nouveau fichier\t\t   3.3 m\tmega.bin");

            Assert.That(line.Kind, Is.EqualTo(LogLineKind.File));
            Assert.That(line.SizeIsExact, Is.False);
            Assert.That(line.Size, Is.EqualTo((long)Math.Round(3.3 * 1024 * 1024)));
            Assert.That(line.Size, Is.InRange(3400000L, 3520000L), "within a few percent of the real 3,500,000");
        }

        [Test]
        [TestCase("10", 10L, true)]
        [TestCase("2500", 2500L, true)]
        [TestCase("2.5 k", 2560L, false)]
        [TestCase("2.5k", 2560L, false)]
        [TestCase("1.1 m", 1153434L, false)]
        [TestCase("1,25 g", 1342177280L, false)]
        [TestCase("2 g", 2147483648L, false)]
        [TestCase("1.5 t", 1649267441664L, false)]
        [TestCase("3.3 M", 3460301L, false)]
        public void Sizes_are_parsed(string text, long expected, bool exact)
        {
            long bytes;
            bool isExact;

            Assert.That(RobocopyLogParser.TryParseSize(text, out bytes, out isExact), Is.True, text);
            Assert.That(bytes, Is.EqualTo(expected), text);
            Assert.That(isExact, Is.EqualTo(exact), text);
        }

        [Test]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("abc")]
        [TestCase("1 2")]
        [TestCase("12 x")]
        [TestCase("-5")]
        [TestCase("m")]
        public void Text_that_is_not_a_size_is_refused(string text)
        {
            long bytes;
            bool exact;

            Assert.That(RobocopyLogParser.TryParseSize(text, out bytes, out exact), Is.False, "[" + text + "]");
        }

        // ------------------------------------------------------------- extras and directories

        [Test]
        public void A_file_that_exists_only_on_the_destination_is_an_extra_in_any_language()
        {
            Assert.That(RobocopyLogParser.Parse("\t  *EXTRA File \t\t       7\tonly-on-nas.txt").Kind,
                Is.EqualTo(LogLineKind.ExtraFile));
            Assert.That(RobocopyLogParser.Parse("\t  *Fichier SUPPL.\t\t       7\tonly-on-nas.txt").Kind,
                Is.EqualTo(LogLineKind.ExtraFile));
        }

        [Test]
        public void A_directory_line_ends_with_a_backslash()
        {
            ParsedLogLine withCount = RobocopyLogParser.Parse("\t  New Dir          2\tC:\\src\\sub\\");
            ParsedLogLine blankClass = RobocopyLogParser.Parse("\t                   3\tC:\\src\\");
            ParsedLogLine french = RobocopyLogParser.Parse("\tNouveau r\u00E9p.       2\tC:\\src\\sub\\");

            Assert.That(withCount.Kind, Is.EqualTo(LogLineKind.Directory));
            Assert.That(withCount.Name, Is.EqualTo("C:\\src\\sub\\"));
            Assert.That(blankClass.Kind, Is.EqualTo(LogLineKind.Directory));
            Assert.That(french.Kind, Is.EqualTo(LogLineKind.Directory));
        }

        [Test]
        public void A_unc_directory_line_is_a_directory()
        {
            Assert.That(RobocopyLogParser.Parse("\t                   3\t\\\\192.168.100.205\\vox\\Folder1\\").Kind,
                Is.EqualTo(LogLineKind.Directory));
        }

        // ------------------------------------------------------------- errors

        [Test]
        public void Timestamped_messages_are_errors_in_any_language()
        {
            Assert.That(RobocopyLogParser.Parse(
                "2026/11/18 08:48:18 ERREUR 2 (0x00000002) Acc\u00E8s au r\u00E9pertoire source C:\\x\\").Kind,
                Is.EqualTo(LogLineKind.Error));
            Assert.That(RobocopyLogParser.Parse(
                "2026/11/18 08:48:18 ERROR 32 (0x00000020) Copying File C:\\x\\y.bin").Kind,
                Is.EqualTo(LogLineKind.Error));
        }

        // ------------------------------------------------------------- everything else

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("-------------------------------------------------------------------------------")]
        [TestCase("   ROBOCOPY   ::   Copie de fichiers robuste pour Windows     ")]
        [TestCase("  D\u00E9but : mercredi 18 novembre 2026 8:45:49 AM")]
        [TestCase("   Source : C:\\Users\\HP\\src\\")]
        [TestCase("  Options : *.* /S /E /DCOPY:T /COPY:DAT /Z /NP /XJ /R:10 /W:30 ")]
        [TestCase("\t    ")]
        [TestCase("               Total     Copi\u00E9    Ignor\u00E9Discordance     \u00C9CHEC    Extras")]
        [TestCase("Fichiers :         5         4         1         0         0         1")]
        [TestCase("  Octets :    4.55 m    4.55 m        10         0         0         7")]
        [TestCase("   D\u00E9bit :           478027700 Octets/sec.")]
        [TestCase("Le fichier sp\u00E9cifi\u00E9 est introuvable.")]
        public void Headers_footers_and_summaries_are_not_result_lines(string text)
        {
            Assert.That(RobocopyLogParser.Parse(text).Kind, Is.EqualTo(LogLineKind.Other), "[" + text + "]");
        }

        [Test]
        public void A_tabbed_line_with_no_readable_size_is_ignored_rather_than_miscounted()
        {
            Assert.That(RobocopyLogParser.Parse("\t  New File  \t\t  oops\tkilo.bin").Kind, Is.EqualTo(LogLineKind.Other));
        }
    }
}
