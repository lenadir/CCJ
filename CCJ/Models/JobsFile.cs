using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace CCJ.Models
{
    /// <summary>Contents of %APPDATA%\CCJ\jobs.json.</summary>
    public class JobsFile
    {
        public const string DefaultSource = @"\\192.168.100.205\vox";
        public const string DefaultNasRoot = @"\\192.168.100.100\3G eng";

        public int Version { get; set; }

        /// <summary>
        /// Root that holds the generated .bat files and the Logs folder. Configurable so the same
        /// build works from the development folder and from C:\Vox on the target machine.
        /// </summary>
        public string VoxRoot { get; set; }

        public List<JobConfig> Jobs { get; set; }

        public JobsFile()
        {
            Version = 1;
            Jobs = new List<JobConfig>();
        }

        [JsonIgnore]
        public string LogsDir { get { return Path.Combine(VoxRoot == null ? "" : VoxRoot, "Logs"); } }

        [JsonIgnore]
        public string MasterLogPath { get { return Path.Combine(LogsDir, "master.log"); } }

        /// <summary>
        /// The default first run: a few minutes from now, rounded to the next five, so a new job
        /// can be scheduled straight away. Any day and any time is allowed; whatever weekday the
        /// first run falls on is the weekday the task then repeats on.
        /// </summary>
        public static DateTime DefaultFirstRun()
        {
            DateTime now = DateTime.Now.AddMinutes(5);
            int minute = now.Minute - (now.Minute % 5);
            return new DateTime(now.Year, now.Month, now.Day, now.Hour, minute, 0);
        }

        public static JobsFile CreateDefault()
        {
            JobsFile file = new JobsFile();
            file.VoxRoot = AppPaths.GuessVoxRoot();
            DateTime firstRun = DefaultFirstRun();

            for (int i = 1; i <= 3; i++)
            {
                string key = "Folder" + i;
                JobConfig job = new JobConfig();
                job.Key = key;
                job.JobName = key;
                job.SourcePath = DefaultSource;
                job.NasRoot = DefaultNasRoot;
                job.NasFolder = key;
                job.FirstRunDate = firstRun;
                job.Enabled = false;
                file.Jobs.Add(job);
            }
            return file;
        }

        /// <summary>
        /// A key no job in the list uses yet, in the same "FolderN" shape as the first three, so
        /// the script it writes is Backup_FolderN.bat.
        /// </summary>
        public string NextFreeKey()
        {
            for (int i = 1; i < 10000; i++)
            {
                string key = "Folder" + i;
                if (FindByKey(key) == null) return key;
            }
            return "Folder" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        /// <summary>
        /// Adds an empty job with a free key. Its paths are left blank on purpose: an untouched new
        /// job is skipped when saving rather than reported as a problem.
        /// </summary>
        public JobConfig AddJob()
        {
            string key = NextFreeKey();

            JobConfig job = new JobConfig();
            job.Key = key;
            job.JobName = key;
            job.SourcePath = "";
            job.NasRoot = Jobs.Count > 0 ? Jobs[0].NasRoot : DefaultNasRoot;
            job.NasFolder = "";
            job.FirstRunDate = DefaultFirstRun();
            job.Enabled = false;

            Jobs.Add(job);
            return job;
        }

        /// <summary>The jobs that have actually been filled in.</summary>
        [JsonIgnore]
        public List<JobConfig> ConfiguredJobs
        {
            get
            {
                List<JobConfig> configured = new List<JobConfig>();
                foreach (JobConfig job in Jobs)
                {
                    if (!job.IsBlank) configured.Add(job);
                }
                return configured;
            }
        }

        /// <summary>
        /// The same settings with every untouched job dropped. This is what gets saved, so a job
        /// the user added and left blank never reaches jobs.json, a script or Task Scheduler.
        /// </summary>
        public JobsFile WithoutBlankJobs()
        {
            JobsFile copy = new JobsFile();
            copy.Version = Version;
            copy.VoxRoot = VoxRoot;
            foreach (JobConfig job in ConfiguredJobs)
                copy.Jobs.Add(job.Clone());
            return copy;
        }

        public JobConfig FindByKey(string key)
        {
            foreach (JobConfig job in Jobs)
            {
                if (string.Equals(job.Key, key, StringComparison.OrdinalIgnoreCase))
                    return job;
            }
            return null;
        }
    }
}
