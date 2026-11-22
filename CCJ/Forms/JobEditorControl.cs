using System;
using System.Collections.Generic;
using System.Windows.Forms;
using CCJ.Models;
using CCJ.Services;

namespace CCJ.Forms
{
    /// <summary>
    /// Editor for one job. It only moves values between the controls and a
    /// <see cref="JobConfig"/>; validation and saving belong to the services.
    /// </summary>
    public partial class JobEditorControl : UserControl
    {
        private string _key = "Folder1";
        private readonly ToolTip _problemTip = new ToolTip();

        public JobEditorControl()
        {
            InitializeComponent();

            this.txtJobName.TextChanged += new EventHandler(OnAnyValueChanged);
            this.txtSource.TextChanged += new EventHandler(OnAnyValueChanged);
            this.txtNasRoot.TextChanged += new EventHandler(OnAnyValueChanged);
            this.txtNasFolder.TextChanged += new EventHandler(OnAnyValueChanged);
            this.dtpFirstRun.ValueChanged += new EventHandler(OnAnyValueChanged);
            this.chkEnabled.CheckedChanged += new EventHandler(OnAnyValueChanged);
        }

        /// <summary>Raised whenever the user changes a field, so the form can refresh previews.</summary>
        public event EventHandler ValueChanged;

        public string JobKey
        {
            get { return _key; }
        }

        /// <summary>Copies a job into the controls.</summary>
        public void Bind(JobConfig job)
        {
            if (job == null) return;

            _key = job.Key;
            this.grpJob.Text = job.Key;
            this.txtJobName.Text = job.JobName;
            this.txtSource.Text = job.SourcePath;
            this.txtNasRoot.Text = job.NasRoot;
            this.txtNasFolder.Text = job.NasFolder;

            DateTime date = job.FirstRunDate;
            if (date < this.dtpFirstRun.MinDate || date > this.dtpFirstRun.MaxDate)
                date = JobsFile.NextSunday();
            this.dtpFirstRun.Value = date.Date;

            this.chkEnabled.Checked = job.Enabled;
            RefreshDerivedLabels();
        }

        /// <summary>Copies the controls back into a job.</summary>
        public void ApplyTo(JobConfig job)
        {
            if (job == null) return;

            job.Key = _key;
            job.JobName = this.txtJobName.Text.Trim();
            job.SourcePath = this.txtSource.Text.Trim();
            job.NasRoot = this.txtNasRoot.Text.Trim();
            job.NasFolder = this.txtNasFolder.Text.Trim();
            job.FirstRunDate = this.dtpFirstRun.Value.Date;
            job.Enabled = this.chkEnabled.Checked;
        }

        /// <summary>A job built from the current control values, without touching the stored one.</summary>
        public JobConfig ToJob()
        {
            JobConfig job = new JobConfig();
            ApplyTo(job);
            return job;
        }

        /// <summary>Shows validation messages under the fields, or clears them when the list is empty.</summary>
        public void ShowProblems(List<string> problems)
        {
            if (problems == null || problems.Count == 0)
            {
                this.lblProblems.Text = "";
                _problemTip.SetToolTip(this.lblProblems, "");
                return;
            }

            this.lblProblems.Text = problems.Count == 1
                ? problems[0]
                : problems.Count + " problems: " + problems[0];

            string all = "";
            foreach (string problem in problems)
                all += "- " + problem + Environment.NewLine;

            _problemTip.SetToolTip(this.lblProblems, all.TrimEnd());
        }

        private void OnAnyValueChanged(object sender, EventArgs e)
        {
            RefreshDerivedLabels();

            if (ValueChanged != null)
                ValueChanged(this, EventArgs.Empty);
        }

        private void RefreshDerivedLabels()
        {
            JobConfig current = ToJob();
            this.lblDestination.Text = current.DestinationPath.Length == 0 ? "-" : current.DestinationPath;

            List<string> problems = Validation.ValidateJob(current);
            ShowProblems(problems);
        }

        private void btnNextSunday_Click(object sender, EventArgs e)
        {
            this.dtpFirstRun.Value = JobsFile.NextSunday();
        }
    }
}
