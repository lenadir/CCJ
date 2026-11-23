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
    ///
    /// The Enabled box is the one control here that acts at once rather than on Save: switching a
    /// job on or off is reported straight away, so the Jobs tab and the Dashboard can never
    /// disagree about which jobs are enabled.
    /// </summary>
    public partial class JobEditorControl : UserControl
    {
        private string _key = "Folder1";
        private readonly ToolTip _problemTip = new ToolTip();
        private bool _binding;

        public JobEditorControl()
        {
            InitializeComponent();

            this.txtJobName.TextChanged += new EventHandler(OnAnyValueChanged);
            this.txtSource.TextChanged += new EventHandler(OnAnyValueChanged);
            this.txtNasRoot.TextChanged += new EventHandler(OnAnyValueChanged);
            this.txtNasFolder.TextChanged += new EventHandler(OnAnyValueChanged);
            this.dtpFirstRun.ValueChanged += new EventHandler(OnAnyValueChanged);
            this.dtpTime.ValueChanged += new EventHandler(OnAnyValueChanged);
            this.chkEnabled.CheckedChanged += new EventHandler(OnEnabledChanged);
        }

        /// <summary>Raised whenever the user changes a field, so the form can refresh previews.</summary>
        public event EventHandler ValueChanged;

        /// <summary>Raised when the user ticks or clears Enabled, which takes effect immediately.</summary>
        public event EventHandler EnabledToggled;

        /// <summary>Raised when the user asks for this job to be removed.</summary>
        public event EventHandler RemoveRequested;

        public string JobKey
        {
            get { return _key; }
        }

        public bool JobEnabled
        {
            get { return this.chkEnabled.Checked; }
        }

        /// <summary>Copies a job into the controls.</summary>
        public void Bind(JobConfig job)
        {
            if (job == null) return;

            _binding = true;
            try
            {
                _key = job.Key;
                this.grpJob.Text = job.Key + (string.IsNullOrEmpty(job.JobName) ? "" : "  -  " + job.JobName);
                this.txtJobName.Text = job.JobName;
                this.txtSource.Text = job.SourcePath;
                this.txtNasRoot.Text = job.NasRoot;
                this.txtNasFolder.Text = job.NasFolder;

                DateTime when = job.FirstRunDate;
                if (when < this.dtpFirstRun.MinDate || when > this.dtpFirstRun.MaxDate)
                    when = JobsFile.NextSunday();

                this.dtpFirstRun.Value = when.Date;
                this.dtpTime.Value = DateTime.Today.Add(when.TimeOfDay);

                this.chkEnabled.Checked = job.Enabled;
            }
            finally
            {
                _binding = false;
            }

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
            job.FirstRunDate = this.dtpFirstRun.Value.Date.Add(this.dtpTime.Value.TimeOfDay);
            job.Enabled = this.chkEnabled.Checked;
        }

        /// <summary>A job built from the current control values, without touching the stored one.</summary>
        public JobConfig ToJob()
        {
            JobConfig job = new JobConfig();
            ApplyTo(job);
            return job;
        }

        /// <summary>
        /// Puts the tick back without raising <see cref="EnabledToggled"/> again, used when the
        /// change could not be saved.
        /// </summary>
        public void SetEnabledQuietly(bool enabled)
        {
            _binding = true;
            try
            {
                this.chkEnabled.Checked = enabled;
            }
            finally
            {
                _binding = false;
            }
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
            if (_binding) return;

            RefreshDerivedLabels();

            if (ValueChanged != null)
                ValueChanged(this, EventArgs.Empty);
        }

        private void OnEnabledChanged(object sender, EventArgs e)
        {
            if (_binding) return;

            if (EnabledToggled != null)
                EnabledToggled(this, EventArgs.Empty);
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (RemoveRequested != null)
                RemoveRequested(this, EventArgs.Empty);
        }

        private void RefreshDerivedLabels()
        {
            JobConfig current = ToJob();

            this.grpJob.Text = _key + (current.JobName.Length == 0 ? "" : "  -  " + current.JobName);
            this.lblDestination.Text = current.DestinationPath.Length == 0 ? "-" : current.DestinationPath;

            // An untouched job is not a problem to be fixed: it is simply skipped when saving.
            if (current.IsBlank)
            {
                ShowProblems(null);
                this.lblDestination.Text = "not set - this job is skipped until you fill it in";
                return;
            }

            ShowProblems(Validation.ValidateJob(current));
        }
    }
}
