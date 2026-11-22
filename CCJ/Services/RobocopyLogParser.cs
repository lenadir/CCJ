using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CCJ.Services
{
    public enum LogLineKind
    {
        /// <summary>Headers, footers, summaries, blank lines: anything that is not a result line.</summary>
        Other,

        /// <summary>A directory robocopy is entering.</summary>
        Directory,

        /// <summary>A file robocopy is copying (or, in a /L pass, would copy).</summary>
        File,

        /// <summary>A file that exists only on the destination. robocopy lists it and leaves it alone.</summary>
        ExtraFile,

        /// <summary>A timestamped error or retry message.</summary>
        Error
    }

    public class ParsedLogLine
    {
        public LogLineKind Kind { get; set; }

        /// <summary>The file name, or the full path in a /NDL pass, or the directory path.</summary>
        public string Name { get; set; }

        public long Size { get; set; }

        /// <summary>False when the size came from an abbreviation such as "3.3 m".</summary>
        public bool SizeIsExact { get; set; }
    }

    /// <summary>
    /// Reads the lines robocopy writes with /LOG. robocopy translates its labels ("New File",
    /// "Nouveau fichier", ...) so nothing here depends on a label. A line is recognised by its
    /// shape instead:
    ///
    ///   - result lines start with a tab and are split into tab separated fields;
    ///   - a directory line ends with a backslash;
    ///   - a file line has the size in the second to last field and the name in the last;
    ///   - a class starting with * marks a file that exists only on the destination;
    ///   - error and retry messages start with a yyyy/MM/dd HH:mm:ss timestamp.
    /// </summary>
    public static class RobocopyLogParser
    {
        private static readonly Regex Timestamped = new Regex(
            @"^\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2} ", RegexOptions.Compiled);

        // 2500, 3.3 m, 1,25 g. robocopy prints whole bytes for small files and an abbreviated
        // number with k / m / g / t (powers of 1024) for large ones.
        private static readonly Regex SizeText = new Regex(
            @"^(\d+(?:[.,]\d+)?)\s*([kKmMgGtT])?$", RegexOptions.Compiled);

        public static ParsedLogLine Parse(string line)
        {
            ParsedLogLine result = new ParsedLogLine();
            result.Kind = LogLineKind.Other;

            if (string.IsNullOrEmpty(line)) return result;

            if (Timestamped.IsMatch(line))
            {
                result.Kind = LogLineKind.Error;
                result.Name = line;
                return result;
            }

            if (line[0] != '\t') return result;

            string[] fields = line.Split('\t');
            if (fields.Length < 3) return result;

            string last = fields[fields.Length - 1];
            if (last.Length == 0) return result;

            if (last.EndsWith("\\", StringComparison.Ordinal))
            {
                result.Kind = LogLineKind.Directory;
                result.Name = last;
                return result;
            }

            if (fields.Length < 4) return result;

            long size;
            bool exact;
            if (!TryParseSize(fields[fields.Length - 2].Trim(), out size, out exact)) return result;

            string kindText = fields[1].Trim();
            result.Kind = kindText.StartsWith("*", StringComparison.Ordinal)
                ? LogLineKind.ExtraFile
                : LogLineKind.File;
            result.Name = last;
            result.Size = size;
            result.SizeIsExact = exact;
            return result;
        }

        /// <summary>
        /// "2500" is exact. "3.3 m" is 3.3 MiB, good to about two digits, which is plenty for a
        /// progress bar.
        /// </summary>
        public static bool TryParseSize(string text, out long bytes, out bool exact)
        {
            bytes = 0;
            exact = false;
            if (string.IsNullOrEmpty(text)) return false;

            Match match = SizeText.Match(text.Trim());
            if (!match.Success) return false;

            string number = match.Groups[1].Value.Replace(',', '.');
            double value;
            if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return false;

            if (!match.Groups[2].Success)
            {
                if (number.IndexOf('.') < 0)
                {
                    long whole;
                    if (!long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out whole))
                        return false;
                    bytes = whole;
                    exact = true;
                    return true;
                }

                bytes = (long)Math.Round(value);
                return true;
            }

            double multiplier = 1;
            switch (char.ToLowerInvariant(match.Groups[2].Value[0]))
            {
                case 'k': multiplier = 1024d; break;
                case 'm': multiplier = 1024d * 1024d; break;
                case 'g': multiplier = 1024d * 1024d * 1024d; break;
                case 't': multiplier = 1024d * 1024d * 1024d * 1024d; break;
            }

            bytes = (long)Math.Round(value * multiplier);
            return true;
        }
    }
}
