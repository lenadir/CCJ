using System;

namespace CCJ.Models
{
    /// <summary>What the dashboard shows for one job, read from Task Scheduler and master.log.</summary>
    public class JobStatus
    {
        public string Key { get; set; }
        public bool TaskExists { get; set; }
        public bool TaskEnabled { get; set; }
        public string TaskState { get; set; }
        public DateTime? NextRun { get; set; }
        public DateTime? LastRun { get; set; }
        public int? LastTaskResult { get; set; }
        public string LastLogLine { get; set; }
        public string Error { get; set; }

        /// <summary>LastTaskResult the way the spec asks for it: 0x0 means success.</summary>
        public string LastResultText
        {
            get
            {
                if (Error != null) return "error";
                if (!TaskExists) return "no task";
                if (!LastTaskResult.HasValue) return "never run";

                int rc = LastTaskResult.Value;
                string hex = "0x" + rc.ToString("X");

                if (rc == 0) return hex + " = success";
                if (rc == 1) return hex + " = incorrect function call";
                if (rc == 2) return hex + " = file not found";
                if (rc == 10) return hex + " = environment incorrect";
                if (rc == 0x41300) return hex + " = ready, not yet run";
                if (rc == 0x41301) return hex + " = currently running";
                if (rc == 0x41303) return hex + " = has never run";
                if (rc == 0x41306) return hex + " = terminated by user";
                return hex + " = failed";
            }
        }

        public string NextRunText
        {
            get { return NextRun.HasValue ? NextRun.Value.ToString("yyyy-MM-dd HH:mm") : "-"; }
        }

        public string LastRunText
        {
            get
            {
                if (!LastRun.HasValue || LastRun.Value.Year < 2000) return "-";
                return LastRun.Value.ToString("yyyy-MM-dd HH:mm");
            }
        }
    }
}
