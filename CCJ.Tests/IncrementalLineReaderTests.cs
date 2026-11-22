using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CCJ.Services;
using NUnit.Framework;

namespace CCJ.Tests
{
    [TestFixture]
    public class IncrementalLineReaderTests
    {
        private string _dir;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = ProgressTestSupport.NewTempDir("CCJ_ReaderTests_");
            _path = Path.Combine(_dir, "run.log");
        }

        [TearDown]
        public void TearDown()
        {
            ProgressTestSupport.DeleteQuietly(_dir);
        }

        private IncrementalLineReader Reader()
        {
            return new IncrementalLineReader(_path, Encoding.GetEncoding(1252));
        }

        [Test]
        public void New_lines_are_returned_once()
        {
            File.WriteAllText(_path, "one\r\ntwo\r\n");
            IncrementalLineReader reader = Reader();

            Assert.That(reader.ReadNewLines(), Is.EqualTo(new string[] { "one", "two" }));
            Assert.That(reader.ReadNewLines(), Is.Empty, "nothing new, so nothing returned");

            ProgressTestSupport.Append(_path, "three\r\n");
            Assert.That(reader.ReadNewLines(), Is.EqualTo(new string[] { "three" }));
        }

        [Test]
        public void A_line_still_being_written_waits_for_its_newline()
        {
            File.WriteAllText(_path, "complete\r\nhalf a li");
            IncrementalLineReader reader = Reader();

            Assert.That(reader.ReadNewLines(), Is.EqualTo(new string[] { "complete" }));

            ProgressTestSupport.Append(_path, "ne\r\n");
            Assert.That(reader.ReadNewLines(), Is.EqualTo(new string[] { "half a line" }));
        }

        [Test]
        public void Lf_only_files_are_read_too()
        {
            File.WriteAllText(_path, "a\nb\nc\n");

            Assert.That(Reader().ReadNewLines(), Is.EqualTo(new string[] { "a", "b", "c" }));
        }

        [Test]
        public void Blank_lines_inside_the_file_are_kept_as_empty_lines()
        {
            File.WriteAllText(_path, "a\r\n\r\nb\r\n");

            Assert.That(Reader().ReadNewLines(), Is.EqualTo(new string[] { "a", "", "b" }));
        }

        [Test]
        public void A_file_still_open_for_writing_can_be_read()
        {
            File.WriteAllText(_path, "first\r\n");

            using (FileStream writer = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                byte[] more = Encoding.ASCII.GetBytes("second\r\n");
                writer.Write(more, 0, more.Length);
                writer.Flush();

                Assert.That(Reader().ReadNewLines(), Is.EqualTo(new string[] { "first", "second" }));
            }
        }

        [Test]
        public void A_file_that_shrinks_is_read_again_from_the_top_and_flagged()
        {
            File.WriteAllText(_path, "old one\r\nold two\r\nold three\r\n");
            IncrementalLineReader reader = Reader();
            reader.ReadNewLines();

            File.WriteAllText(_path, "new\r\n");
            List<string> lines = reader.ReadNewLines();

            Assert.That(reader.WasReset, Is.True);
            Assert.That(lines, Is.EqualTo(new string[] { "new" }));
        }

        [Test]
        public void A_log_rewritten_to_a_larger_size_is_still_noticed()
        {
            // The script reuses one log name for every attempt, and robocopy overwrites it. By the
            // next look the new log can already be longer than the old one, so length alone is not enough.
            File.WriteAllText(_path, ProgressTestSupport.Header("Wed Nov 18 2026 8:00:00 AM") +
                ProgressTestSupport.FileLine("a.bin", 10));
            IncrementalLineReader reader = Reader();
            reader.ReadNewLines();

            File.WriteAllText(_path, ProgressTestSupport.Header("Wed Nov 18 2026 8:15:07 AM") +
                ProgressTestSupport.FileLine("a.bin", 10) + ProgressTestSupport.FileLine("b.bin", 20) +
                ProgressTestSupport.FileLine("c.bin", 30));

            List<string> lines = reader.ReadNewLines();

            Assert.That(reader.WasReset, Is.True, "the Started line changed, so this is a different log");
            Assert.That(lines.FindAll(delegate(string l) { return l.Contains(".bin"); }).Count, Is.EqualTo(3));
        }

        [Test]
        public void A_log_that_only_grows_is_not_mistaken_for_a_rewrite()
        {
            File.WriteAllText(_path, ProgressTestSupport.Header("Wed Nov 18 2026 8:00:00 AM"));
            IncrementalLineReader reader = Reader();
            reader.ReadNewLines();

            for (int i = 0; i < 5; i++)
            {
                ProgressTestSupport.Append(_path, ProgressTestSupport.FileLine("f" + i + ".bin", 100));
                List<string> lines = reader.ReadNewLines();

                Assert.That(reader.WasReset, Is.False, "append number " + i);
                Assert.That(lines.Count, Is.EqualTo(1));
            }
        }

        [Test]
        public void Accented_characters_in_the_log_are_decoded_with_the_given_encoding()
        {
            File.WriteAllBytes(_path, new byte[] { 0xE9, 0x74, 0xE9, 0x0D, 0x0A }); // "ete" with acute accents, in Windows-1252

            Assert.That(Reader().ReadNewLines()[0], Is.EqualTo("été"));
        }

        [Test]
        public void A_file_that_does_not_exist_raises_a_file_error_the_caller_can_handle()
        {
            Assert.Throws<FileNotFoundException>(delegate { Reader().ReadNewLines(); });
        }

        [Test]
        public void An_empty_file_gives_no_lines()
        {
            File.WriteAllText(_path, "");

            Assert.That(Reader().ReadNewLines(), Is.Empty);
        }
    }
}
