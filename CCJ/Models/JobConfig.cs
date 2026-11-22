using System;
using System.IO;
using Newtonsoft.Json;

namespace CCJ.Models
{
    /// <summary>
    /// One of the three fixed backup jobs. Key is fixed ("Folder1".."Folder3") and decides the
    /// script file name; every other field is editable.
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
