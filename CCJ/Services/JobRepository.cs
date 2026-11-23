using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CCJ.Models;
using Newtonsoft.Json;

namespace CCJ.Services
{
    /// <summary>
    /// Loads and saves %APPDATA%\CCJ\jobs.json. Nothing is written unless it validates, and the
    /// write is staged through a temporary file so a crash cannot leave a half written settings
    /// file behind.
    /// </summary>
    public class JobRepository
    {
        private readonly string _path;

        public JobRepository()
            : this(AppPaths.JobsFilePath)
        {
        }

        public JobRepository(string path)
        {
            _path = path;
        }

        public string Path { get { return _path; } }

        public bool Exists { get { return File.Exists(_path); } }

        /// <summary>
        /// The stored jobs, or freshly seeded defaults when there is no file yet. A corrupt file is
        /// renamed to jobs.json.bad rather than silently replaced, so nothing is lost.
        /// </summary>
        public JobsFile Load()
        {
            if (!File.Exists(_path))
            {
                AppLog.Info("No jobs.json at " + _path + ", starting from defaults.");
                return JobsFile.CreateDefault();
            }

            string json;
            try
            {
                json = File.ReadAllText(_path, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not read " + _path, ex);
                throw;
            }

            JobsFile file;
            try
            {
                file = JsonConvert.DeserializeObject<JobsFile>(json);
            }
            catch (Exception ex)
            {
                AppLog.Error("jobs.json is not valid JSON, keeping a copy as jobs.json.bad", ex);
                QuarantineBadFile();
                return JobsFile.CreateDefault();
            }

            if (file == null)
            {
                QuarantineBadFile();
                return JobsFile.CreateDefault();
            }

            return Repair(file);
        }

        /// <summary>
        /// Makes a loaded file usable without changing what the user chose. Every stored job is
        /// kept, in its stored order, however many there are: the number of jobs is the user's, not
        /// the program's. Only genuinely unusable parts are filled in - a missing Vox root, a
        /// missing or duplicated key, a date that was never set.
        ///
        /// A file with no jobs at all gets the three starter jobs, which is what a brand new
        /// installation sees.
        /// </summary>
        private static JobsFile Repair(JobsFile file)
        {
            if (file.Jobs == null)
                file.Jobs = new List<JobConfig>();

            if (string.IsNullOrEmpty(file.VoxRoot))
                file.VoxRoot = AppPaths.GuessVoxRoot();

            if (file.Jobs.Count == 0)
            {
                JobsFile defaults = JobsFile.CreateDefault();
                file.Jobs = defaults.Jobs;
            }
            else
            {
                HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (JobConfig job in file.Jobs)
                {
                    string key = job.Key == null ? "" : job.Key.Trim();
                    if (key.Length == 0 || used.Contains(key))
                        key = NextFreeKey(used);

                    job.Key = key;
                    used.Add(key);

                    if (string.IsNullOrEmpty(job.JobName)) job.JobName = key;
                    if (job.SourcePath == null) job.SourcePath = "";
                    if (job.NasRoot == null) job.NasRoot = "";
                    if (job.NasFolder == null) job.NasFolder = "";
                    if (job.FirstRunDate == default(DateTime))
                        job.FirstRunDate = JobsFile.NextSunday();
                }
            }

            if (file.Version <= 0) file.Version = 1;
            return file;
        }

        private static string NextFreeKey(HashSet<string> used)
        {
            for (int i = 1; i < 10000; i++)
            {
                string candidate = "Folder" + i;
                if (!used.Contains(candidate)) return candidate;
            }
            return "Folder" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private void QuarantineBadFile()
        {
            try
            {
                string bad = _path + ".bad";
                if (File.Exists(bad)) File.Delete(bad);
                File.Move(_path, bad);
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not move the unreadable jobs.json aside.", ex);
            }
        }

        /// <summary>
        /// Validates and saves. Throws <see cref="ValidationException"/> with every problem found
        /// if anything is wrong, and in that case writes nothing.
        /// </summary>
        public void Save(JobsFile file)
        {
            if (file == null) throw new ArgumentNullException("file");

            List<string> errors = Validation.ValidateFile(file);
            if (errors.Count > 0)
                throw new ValidationException(errors);

            AppPaths.EnsureDataDir();

            string dir = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonConvert.SerializeObject(file, Formatting.Indented);
            string temp = _path + ".tmp";

            File.WriteAllText(temp, json, new UTF8Encoding(false));

            if (File.Exists(_path))
                File.Replace(temp, _path, null);
            else
                File.Move(temp, _path);

            AppLog.Info("Saved " + file.Jobs.Count + " jobs to " + _path);
        }
    }

    /// <summary>Carries every validation message so the form can show them all at once.</summary>
    public class ValidationException : Exception
    {
        public List<string> Errors { get; private set; }

        public ValidationException(List<string> errors)
            : base(BuildMessage(errors))
        {
            Errors = errors == null ? new List<string>() : errors;
        }

        private static string BuildMessage(List<string> errors)
        {
            if (errors == null || errors.Count == 0)
                return "The settings are not valid.";

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Nothing was saved. Please fix these first:");
            builder.AppendLine();
            foreach (string error in errors)
                builder.AppendLine("  - " + error);
            return builder.ToString();
        }
    }
}
