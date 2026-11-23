using System;
using System.IO;
using Newtonsoft.Json;

namespace CCJ.Models
{
    /// <summary>
    /// One backup job. Key ("Folder1", "Folder2", ...) decides the script file name and stays with
    /// the job for its whole life; every other field is editable. There can be as many jobs as the
    /// user adds, and a job that has been added but not filled in yet is simply ignored.
    /// </summary>
    public class JobConfig
    {
        public string Key { get; set; }
        public string JobName { get; set; }
        public string SourcePath { get; set; }
        public string NasRoot { get; set; }
        public string NasFolder { get; set; }
        public DateTime FirstRunDate { get; set; }
        public bool Enabled { get; set; }

        /// <summary>NAS root plus folder, for example \\192.168.100.100\3G eng\Folder1</summary>
        [JsonIgnore]
        public string DestinationPath
        {
            get
            {
                string root = (NasRoot == null ? "" : NasRoot.TrimEnd('\\'));
                string folder = (NasFolder == null ? "" : NasFolder.Trim().Trim('\\'));
                if (root.Length == 0) return folder;
                if (folder.Length == 0) return root;
                return root + "\\" + folder;
            }
        }

        /// <summary>Backup_Folder1.bat</summary>
        [JsonIgnore]
        public string ScriptFileName { get { return "Backup_" + Key + ".bat"; } }

        /// <summary>
        /// True while neither the source nor the NAS folder name has been filled in. Such a job is
        /// one the user added and left alone: it is skipped when saving instead of being reported
        /// as a problem, so one filled in job is enough to save and to start a copy.
        ///
        /// The NAS root does not count, because a new job is offered with the NAS root of the first
        /// job already filled in: that is a suggestion, not something the user typed.
        /// </summary>
        [JsonIgnore]
        public bool IsBlank
        {
            get
            {
                return IsEmpty(SourcePath) && IsEmpty(NasFolder);
            }
        }

        private static bool IsEmpty(string value)
        {
            return value == null || value.Trim().Length == 0;
        }

        /// <summary>The Task Scheduler task name, for example "CCJ Backup Folder1".</summary>
        [JsonIgnore]
        public string TaskName { get { return "CCJ Backup " + (JobName == null ? Key : JobName); } }

        public string ScriptPath(string voxRoot)
        {
            return Path.Combine(voxRoot, ScriptFileName);
        }

        public JobConfig Clone()
        {
            JobConfig copy = new JobConfig();
            copy.Key = Key;
            copy.JobName = JobName;
            copy.SourcePath = SourcePath;
            copy.NasRoot = NasRoot;
            copy.NasFolder = NasFolder;
            copy.FirstRunDate = FirstRunDate;
            copy.Enabled = Enabled;
            return copy;
        }
    }
}
