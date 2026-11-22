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

        /// <summary>The next Sunday at 00:00, used as the default first run date.</summary>
        public static DateTime NextSunday()
        {
            DateTime d = DateTime.Today.AddDays(1);
            while (d.DayOfWeek != DayOfWeek.Sunday)
                d = d.AddDays(1);
            return d.Date;
        }

        public static JobsFile CreateDefault()
        {
            JobsFile file = new JobsFile();
            file.VoxRoot = AppPaths.GuessVoxRoot();
            DateTime sunday = NextSunday();

            for (int i = 1; i <= 3; i++)
            {
                string key = "Folder" + i;
                JobConfig job = new JobConfig();
                job.Key = key;
                job.JobName = key;
                job.SourcePath = DefaultSource;
                job.NasRoot = DefaultNasRoot;
                job.NasFolder = key;
                job.FirstRunDate = sunday;
                job.Enabled = false;
                file.Jobs.Add(job);
            }
            return file;
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
