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
        /// Makes sure a loaded file always has the three fixed jobs and a usable Vox root, so the
        /// UI never has to cope with a half filled settings file.
        /// </summary>
        private static JobsFile Repair(JobsFile file)
        {
            if (file.Jobs == null)
                file.Jobs = new List<JobConfig>();

            if (string.IsNullOrEmpty(file.VoxRoot))
                file.VoxRoot = AppPaths.GuessVoxRoot();

            JobsFile defaults = JobsFile.CreateDefault();
            List<JobConfig> ordered = new List<JobConfig>();

            foreach (JobConfig template in defaults.Jobs)
            {
                JobConfig existing = file.FindByKey(template.Key);
                if (existing == null)
                {
                    ordered.Add(template);
                    continue;
                }

                existing.Key = template.Key;
                if (string.IsNullOrEmpty(existing.JobName)) existing.JobName = template.JobName;
                if (string.IsNullOrEmpty(existing.SourcePath)) existing.SourcePath = template.SourcePath;
                if (string.IsNullOrEmpty(existing.NasRoot)) existing.NasRoot = template.NasRoot;
                if (string.IsNullOrEmpty(existing.NasFolder)) existing.NasFolder = template.NasFolder;
                if (existing.FirstRunDate == default(DateTime))
                    existing.FirstRunDate = template.FirstRunDate;

                ordered.Add(existing);
            }

            file.Jobs = ordered;
            if (file.Version <= 0) file.Version = 1;
            return file;
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
