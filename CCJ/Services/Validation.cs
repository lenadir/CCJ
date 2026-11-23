using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CCJ.Models;

namespace CCJ.Services
{
    /// <summary>
    /// Field validation for jobs. Every rule here is checked before anything is written to
    /// jobs.json, to a .bat file or to Task Scheduler.
    /// </summary>
    public static class Validation
    {
        /// <summary>Characters the spec forbids in a NAS folder name.</summary>
        public static readonly char[] InvalidFolderChars =
            new char[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

        private static readonly string[] ReservedNames = new string[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        // \\host\share and optionally more segments. The share may contain spaces, as in
        // \\192.168.100.100\3G eng, so spaces are allowed inside a segment but not as a
        // whole segment.
        private static readonly Regex UncRegex = new Regex(
            @"^\\\\[A-Za-z0-9](?:[A-Za-z0-9._\-]*[A-Za-z0-9])?" +   // host or IP
            @"(?:\\[^\\/:*?""<>|]*[^\\/:*?""<>|\s])+" +             // one or more segments
            @"\\?$",
            RegexOptions.Compiled);

        /// <summary>True for \\host\share, optionally followed by further path segments.</summary>
        public static bool IsValidUncPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (path.Length > 240) return false;
            if (path.IndexOf('/') >= 0) return false;
            return UncRegex.IsMatch(path);
        }

        /// <summary>The first run must be a Sunday, which the weekly trigger then repeats.</summary>
        public static bool IsSunday(DateTime date)
        {
            return date.DayOfWeek == DayOfWeek.Sunday;
        }

        /// <summary>
        /// A single folder name under the NAS root: no path separators, no characters Windows
        /// forbids, not a reserved device name, not dots or spaces only.
        /// </summary>
        public static bool IsValidFolderName(string name)
        {
            if (name == null) return false;
            if (name.Length == 0 || name.Length > 120) return false;
            if (name != name.Trim()) return false;
            if (name.IndexOfAny(InvalidFolderChars) >= 0) return false;

            foreach (char c in name)
            {
                if (c < 32) return false;
            }

            if (name.TrimEnd('.', ' ').Length == 0) return false;
            if (name.EndsWith(".") || name.EndsWith(" ")) return false;

            string bare = name;
            int dot = bare.IndexOf('.');
            if (dot > 0) bare = bare.Substring(0, dot);
            foreach (string reserved in ReservedNames)
            {
                if (string.Equals(bare, reserved, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// All problems with one job, so the form can show them together instead of one at a time.
        /// An empty list means the job is safe to save.
        /// </summary>
        public static List<string> ValidateJob(JobConfig job)
        {
            List<string> errors = new List<string>();
            if (job == null)
            {
                errors.Add("No job supplied.");
                return errors;
            }

            if (string.IsNullOrEmpty(job.JobName) || job.JobName.Trim().Length == 0)
                errors.Add("Job name must not be empty.");
            else if (job.JobName.IndexOfAny(new char[] { '\\', '/', ':' }) >= 0)
                errors.Add("Job name must not contain \\ / or : because it becomes the task name.");

            if (!IsValidUncPath(job.SourcePath))
                errors.Add("Source must be a UNC path in the form \\\\server\\share, for example " +
                           JobsFile.DefaultSource + ".");

            if (!IsValidUncPath(job.NasRoot))
                errors.Add("NAS root must be a UNC path in the form \\\\server\\share, for example " +
                           JobsFile.DefaultNasRoot + ".");

            if (!IsValidFolderName(job.NasFolder))
                errors.Add("NAS folder name is not valid. It must be a single folder name with none of " +
                           "these characters: \\ / : * ? \" < > |");

            if (!IsSunday(job.FirstRunDate))
                errors.Add("First run date must be a Sunday, because the task repeats weekly on " +
                           "a Sunday. " + job.FirstRunDate.ToString("yyyy-MM-dd") + " is a " +
                           job.FirstRunDate.DayOfWeek + ".");

            return errors;
        }

        /// <summary>All problems across the whole file, keyed by job for display.</summary>
        public static List<string> ValidateFile(JobsFile file)
        {
            List<string> errors = new List<string>();
            if (file == null)
            {
                errors.Add("No settings supplied.");
                return errors;
            }

            if (string.IsNullOrEmpty(file.VoxRoot))
            {
                errors.Add("Script folder (Vox root) must not be empty.");
            }
            else
            {
                try
                {
                    System.IO.Path.GetFullPath(file.VoxRoot);
                }
                catch (Exception)
                {
                    errors.Add("Script folder (Vox root) is not a valid path: " + file.VoxRoot);
                }
            }

            if (file.Jobs.Count == 0)
                errors.Add("There are no jobs. Add at least one job on the Jobs tab.");

            foreach (JobConfig job in file.Jobs)
            {
                foreach (string error in ValidateJob(job))
                    errors.Add(job.Key + ": " + error);
            }

            errors.AddRange(FindDuplicates(file));
            return errors;
        }

        /// <summary>
        /// Two jobs that share a key would write the same .bat file, and two that share a job name
        /// would fight over one scheduled task, so both are refused before anything is written.
        /// </summary>
        private static List<string> FindDuplicates(JobsFile file)
        {
            List<string> errors = new List<string>();
            Dictionary<string, string> keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (JobConfig job in file.Jobs)
            {
                string key = job.Key == null ? "" : job.Key.Trim();
                if (key.Length == 0)
                {
                    errors.Add("A job has no key, so its script would have no name.");
                }
                else if (keys.ContainsKey(key))
                {
                    errors.Add(key + ": two jobs have the key " + key +
                               ", so they would both write " + job.ScriptFileName + ".");
                }
                else
                {
                    keys.Add(key, key);
                }

                string name = job.JobName == null ? "" : job.JobName.Trim();
                if (name.Length == 0) continue;

                if (names.ContainsKey(name))
                {
                    errors.Add(key + ": the job name \"" + name + "\" is already used by " +
                               names[name] + ". Each job needs its own name, because the name " +
                               "becomes the scheduled task name.");
                }
                else
                {
                    names.Add(name, key);
                }
            }
            return errors;
        }
    }
}
