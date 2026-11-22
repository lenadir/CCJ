using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CCJ.Services
{
    /// <summary>
    /// Reads master.log without locking it, so a running backup script can keep appending while
    /// the viewer is open.
    /// </summary>
    public class LogReader
    {
        public const int DefaultLineCount = 200;

        /// <summary>Only the tail of the file is read, however big it has grown.</summary>
        private const int MaxBytesToRead = 1024 * 1024;

        /// <summary>
        /// The last <paramref name="maxLines"/> lines, oldest first. If filterTerms are given,
        /// only lines containing at least one of them are returned.
        /// </summary>
        public string[] ReadLastLines(string path, int maxLines, params string[] filterTerms)
        {
            if (maxLines <= 0) maxLines = DefaultLineCount;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return new string[0];

            string text = ReadTail(path);
            string[] all = text.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

            List<string> kept = new List<string>();
            foreach (string line in all)
            {
                if (line.Trim().Length == 0) continue;
                if (Matches(line, filterTerms))
                    kept.Add(line);
            }

            if (kept.Count <= maxLines)
                return kept.ToArray();

            return kept.GetRange(kept.Count - maxLines, maxLines).ToArray();
        }

        /// <summary>
        /// The most recent line for one job. The generated script logs the NAS folder name, which
        /// is why the job name and the folder name are both accepted.
        /// </summary>
        public string LastLineFor(string path, params string[] filterTerms)
        {
            string[] lines = ReadLastLines(path, 1, filterTerms);
            return lines.Length == 0 ? "" : lines[lines.Length - 1];
        }

        private static bool Matches(string line, string[] filterTerms)
        {
            if (filterTerms == null || filterTerms.Length == 0) return true;

            bool anyRealTerm = false;
            foreach (string term in filterTerms)
            {
                if (string.IsNullOrEmpty(term)) continue;
                anyRealTerm = true;
                if (line.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return !anyRealTerm;
        }

        private static string ReadTail(string path)
        {
            using (FileStream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                bool truncated = false;
                if (stream.Length > MaxBytesToRead)
                {
                    stream.Seek(-MaxBytesToRead, SeekOrigin.End);
                    truncated = true;
                }

                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    string text = reader.ReadToEnd();
                    if (truncated)
                    {
                        // The first line is probably cut in half, so drop it.
                        int firstBreak = text.IndexOf('\n');
                        if (firstBreak >= 0 && firstBreak + 1 < text.Length)
                            text = text.Substring(firstBreak + 1);
                    }
                    return text;
                }
            }
        }
    }
}
