using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CCJ.Models;
using CCJ.Services;

namespace CCJ.Forms
{
    /// <summary>
    /// The one window. It holds no backup logic of its own: every action delegates to a service
    /// and the form only moves values around, reports progress and shows errors.
    /// </summary>
    public partial class MainForm : Form
    {
        private readonly JobRepository _repository = new JobRepository();
        private readonly ScriptGenerator _generator = new ScriptGenerator();
        private readonly TaskSchedulerService _tasks = new TaskSchedulerService();
        private readonly ConnectivityChecker _connectivity = new ConnectivityChecker();
        private readonly CredentialChecker _credentials = new CredentialChecker();
        private readonly LogReader _logs = new LogReader();
        private readonly ProgressService _progress;

        private const string NoProgressText =
            "Select a job to see how far it has got. A backup that is running, including a scheduled " +
            "one that started while CCJ was closed, shows up here by itself.";

        private readonly List<JobEditorControl> _editors = new List<JobEditorControl>();
        private readonly UiSettings _ui;
        private JobsFile _file;
        private bool _loading;

        /// <summary>The cmd.exe of a dry run CCJ started itself, so Stop can end it.</summary>
        private Process _dryRunProcess;

        public MainForm()
        {
            _ui = UiSettings.Load();
            Theme.Use(_ui.DarkMode);

            InitializeComponent();
            BuildGridColumns();

            // Ticking the box here must not look like the user switching it.
            _loading = true;
            try
            {
                this.chkDarkMode.Checked = _ui.DarkMode;
            }
            finally
            {
                _loading = false;
            }

            // Follows running backups on a timer of its own. The form only receives the results.
            _progress = new ProgressService(
                delegate { return _file; },
                delegate(JobConfig job) { return _tasks.GetRunInfo(job); });
            _progress.Changed += new EventHandler<ProgressEventArgs>(OnProgressChanged);

            this.lblProgressDetail.Text = NoProgressText;
            ApplyTheme();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Stops the timer and ends any list-only robocopy that is still counting.
            _progress.Dispose();
            base.OnFormClosed(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            this.pnlElevation.Visible = !Elevation.IsElevated();

            try
            {
                _file = _repository.Load();
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not load jobs.json, starting from defaults.", ex);
                ShowError("Could not read your saved settings, so defaults are shown.", ex);
                _file = JobsFile.CreateDefault();
            }

            BindFile();
            RefreshDashboard();
            RefreshLogView();
            SetStatus("Loaded " + _file.Jobs.Count + " job(s) from " + _repository.Path);

            // Every 1.5 seconds; it also looks for a task that is already running.
            _progress.Start(1500);
        }

        // ---------------------------------------------------------------- setup

        /// <summary>
        /// One editor per job, stacked top down inside a scrolling panel. Rebuilt whenever the
        /// number of jobs changes, because the user can add and remove jobs.
        /// </summary>
        private void RebuildJobEditors()
        {
            this.pnlJobEditors.SuspendLayout();
            try
            {
                foreach (JobEditorControl old in _editors)
                {
                    this.pnlJobEditors.Controls.Remove(old);
                    old.Dispose();
                }
                _editors.Clear();

                // Docked to the top, so the one added last ends up highest: add them backwards and
                // the first job sits at the top of the panel.
                for (int i = _file.Jobs.Count - 1; i >= 0; i--)
                {
                    JobEditorControl editor = new JobEditorControl();
                    editor.Dock = DockStyle.Top;
                    editor.Height = 212;
                    editor.Bind(_file.Jobs[i]);
                    editor.ValueChanged += new EventHandler(OnEditorValueChanged);
                    editor.EnabledToggled += new EventHandler(OnEditorEnabledToggled);
                    editor.RemoveRequested += new EventHandler(OnEditorRemoveRequested);

                    this.pnlJobEditors.Controls.Add(editor);
                    _editors.Insert(0, editor);
                }
            }
            finally
            {
                this.pnlJobEditors.ResumeLayout(true);
            }

            Theme.Apply(this.pnlJobEditors);
        }

        private void BuildGridColumns()
        {
            this.grid.AutoGenerateColumns = false;
            this.grid.Columns.Clear();

            AddColumn("Key", "Job", 70);
            AddColumn("Source", "Source", 150);
            AddColumn("Destination", "Destination", 150);
            AddColumn("Enabled", "Enabled", 60);
            AddColumn("Task", "Task", 70);

            DataGridViewProgressColumn progress = new DataGridViewProgressColumn();
            progress.Name = "Progress";
            progress.HeaderText = "Progress";
            progress.Width = 250;
            this.grid.Columns.Add(progress);

            AddColumn("NextRun", "Next run", 110);
            AddColumn("LastRun", "Last run", 110);
            AddColumn("LastResult", "Last result", 150);
            AddColumn("LastLog", "Last log line for this job", 420);
        }

        private void AddColumn(string name, string header, int width)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.Width = width;
            column.ReadOnly = true;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            this.grid.Columns.Add(column);
        }

        private void BindFile()
        {
            _loading = true;
            try
            {
                this.txtVoxRoot.Text = _file.VoxRoot;

                RebuildJobEditors();

                this.cmbLogFilter.Items.Clear();
                this.cmbLogFilter.Items.Add("All jobs");
                foreach (JobConfig job in _file.Jobs)
                    this.cmbLogFilter.Items.Add(job.Key);
                this.cmbLogFilter.SelectedIndex = 0;
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>The jobs as currently shown in the editors, without saving anything.</summary>
        private JobsFile CollectFromUi()
        {
            JobsFile collected = new JobsFile();
            collected.Version = _file.Version;
            collected.VoxRoot = this.txtVoxRoot.Text.Trim();

            foreach (JobEditorControl editor in _editors)
            {
                JobConfig stored = _file.FindByKey(editor.JobKey);
                JobConfig job = stored != null ? stored.Clone() : new JobConfig();
                editor.ApplyTo(job);
                collected.Jobs.Add(job);
            }
            return collected;
        }

        private void OnEditorValueChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            SetStatus("Unsaved changes. Use Save to write the scripts and update the tasks.");
        }

        // ---------------------------------------------------------------- adding and removing jobs

        private void btnAddJob_Click(object sender, EventArgs e)
        {
            // Everything typed so far is kept, so adding a job never loses an edit in progress.
            JobsFile collected = CollectFromUi();
            collected.AddJob();

            _file = collected;
            BindFile();

            this.tabs.SelectedTab = this.tabJobs;
            if (_editors.Count > 0)
                _editors[_editors.Count - 1].Focus();

            RefreshDashboard();
            SetStatus("Added " + _file.Jobs[_file.Jobs.Count - 1].Key +
                      ". Fill in its source, NAS root and folder, then use Save.");
        }

        private void OnEditorRemoveRequested(object sender, EventArgs e)
        {
            JobEditorControl editor = sender as JobEditorControl;
            if (editor == null) return;

            JobConfig shown = editor.ToJob();
            bool blank = shown.IsBlank;

            if (!blank)
            {
                DialogResult answer = MessageBox.Show(this,
                    "Remove the job \"" + shown.JobName + "\" (" + editor.JobKey + ") from CCJ?\r\n\r\n" +
                    "Its scheduled task is deleted if it exists. The script file and every log stay " +
                    "where they are, and nothing that has already been copied is touched.",
                    "Remove job", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (answer != DialogResult.Yes) return;
            }

            if (_progress.IsActive(editor.JobKey))
            {
                MessageBox.Show(this,
                    "\"" + shown.JobName + "\" is running at the moment. Stop it first, then remove it.",
                    "Still running", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            JobsFile collected = CollectFromUi();
            JobConfig toRemove = collected.FindByKey(editor.JobKey);
            if (toRemove == null) return;

            collected.Jobs.Remove(toRemove);

            if (collected.ConfiguredJobs.Count == 0)
            {
                MessageBox.Show(this,
                    "That is the last job that has been filled in. CCJ needs at least one job, so " +
                    "this one was kept. Change its paths instead, or add another job first.",
                    "At least one job is needed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // The settings file is written straight away, so a removal survives closing the window.
            if (!blank && !TrySave(collected, "the job was not removed"))
                return;

            string note = "";
            if (!blank)
            {
                try
                {
                    note = _tasks.Delete(toRemove)
                        ? " Its scheduled task was deleted."
                        : " It had no scheduled task.";
                }
                catch (Exception ex)
                {
                    AppLog.Error("Could not delete the task for " + toRemove.Key, ex);
                    note = " Its scheduled task could not be deleted: " + ex.Message;
                }
            }

            _file = blank ? collected : _repository.Load();
            BindFile();
            _progress.Untrack(toRemove.Key);
            RefreshDashboard();
            RefreshLogView();

            SetStatus("Removed " + toRemove.Key + "." + note);
        }

        /// <summary>
        /// Saves the blank jobs out and reports any problem as a validation message. Returns false
        /// when nothing was written.
        /// </summary>
        private bool TrySave(JobsFile collected, string whatFailed)
        {
            try
            {
                _repository.Save(collected.WithoutBlankJobs());
                return true;
            }
            catch (ValidationException ex)
            {
                ShowValidationErrors(ex.Errors);
                SetStatus("Nothing was saved, so " + whatFailed + ".");
                return false;
            }
            catch (Exception ex)
            {
                ShowError("The settings could not be saved, so " + whatFailed + ".", ex);
                return false;
            }
        }

        // ---------------------------------------------------------------- enable / disable

        /// <summary>
        /// The Enabled box on the Jobs tab is the only switch for a job, and it acts at once: the
        /// settings file and the scheduled task are both updated, and the Dashboard is refreshed,
        /// so the two tabs always show the same thing.
        /// </summary>
        private void OnEditorEnabledToggled(object sender, EventArgs e)
        {
            JobEditorControl editor = sender as JobEditorControl;
            if (editor == null || _loading) return;

            JobConfig stored = _file.FindByKey(editor.JobKey);
            bool wanted = editor.JobEnabled;

            if (stored == null) return;

            if (stored.IsBlank)
            {
                editor.SetEnabledQuietly(false);
                MessageBox.Show(this,
                    "Fill in the source, the NAS root and the folder name first, then use Save. " +
                    "A job that has not been filled in cannot be enabled.",
                    "Nothing to enable yet", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool previous = stored.Enabled;
            stored.Enabled = wanted;

            if (!TrySave(_file, "the job was left " + (previous ? "enabled" : "disabled")))
            {
                stored.Enabled = previous;
                editor.SetEnabledQuietly(previous);
                return;
            }

            string note;
            try
            {
                note = _tasks.SetEnabled(stored, wanted)
                    ? " The scheduled task was updated too."
                    : " There is no scheduled task yet, so use Save to create it.";
            }
            catch (UnauthorizedAccessException)
            {
                note = " The scheduled task could not be changed: that needs administrator rights.";
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not switch the task for " + stored.Key, ex);
                note = " The scheduled task could not be changed: " + ex.Message;
            }

            RefreshDashboard();
            SetStatus(stored.Key + " is now " + (wanted ? "enabled" : "disabled") + "." + note);
        }

        // ---------------------------------------------------------------- dashboard

        private void RefreshDashboard()
        {
            try
            {
                JobsFile shown = _file;
                string selectedKey = SelectedKeyQuietly();
                this.grid.Rows.Clear();

                foreach (JobConfig job in shown.Jobs)
                {
                    JobStatus status = job.IsBlank ? new JobStatus() : _tasks.GetStatus(job);
                    string lastLine = "";
                    try
                    {
                        if (!job.IsBlank)
                            lastLine = _logs.LastLineFor(shown.MasterLogPath, job.Key, job.NasFolder, job.JobName);
                    }
                    catch (Exception ex)
                    {
                        AppLog.Warn("Could not read master.log: " + ex.Message);
                    }

                    CopyProgress progress = _progress.Latest(job.Key);

                    int index = this.grid.Rows.Add(
                        job.Key,
                        job.IsBlank ? "(not filled in)" : job.SourcePath,
                        job.IsBlank ? "" : job.DestinationPath,
                        job.Enabled ? "yes" : "no",
                        job.IsBlank ? "-" : (status.TaskExists ? (status.TaskEnabled ? "enabled" : "disabled") : "missing"),
                        progress,
                        status.NextRunText,
                        status.LastRunText,
                        status.LastResultText,
                        lastLine);

                    DataGridViewRow row = this.grid.Rows[index];
                    row.Tag = job.Key;
                    if (progress != null) row.Cells["Progress"].ToolTipText = progress.Detail;

                    if (job.IsBlank)
                    {
                        row.Cells["Source"].Style.ForeColor = Theme.SubtleText;
                    }
                    else if (!status.TaskExists)
                    {
                        row.Cells["Task"].Style.ForeColor = Theme.ErrorText;
                    }
                    else if (!status.TaskEnabled)
                    {
                        row.Cells["Task"].Style.ForeColor = Theme.WarningText;
                    }

                    if (status.LastTaskResult.HasValue && status.LastTaskResult.Value != 0 &&
                        status.LastTaskResult.Value != 0x41300 && status.LastTaskResult.Value != 0x41303)
                    {
                        row.Cells["LastResult"].Style.ForeColor = Theme.ErrorText;
                    }
                }

                SelectRow(selectedKey);
                UpdateProgressDetail();
                UpdateRunButtons();
            }
            catch (Exception ex)
            {
                ShowError("Could not refresh the dashboard.", ex);
            }
        }

        /// <summary>Puts the selection back on a job after the grid was rebuilt.</summary>
        private void SelectRow(string key)
        {
            if (key == null) return;
            foreach (DataGridViewRow row in this.grid.Rows)
            {
                if (string.Equals(Convert.ToString(row.Tag), key, StringComparison.OrdinalIgnoreCase))
                {
                    this.grid.ClearSelection();
                    row.Selected = true;
                    return;
                }
            }
        }

        /// <summary>The selected job's key, with no message box when nothing is selected.</summary>
        private string SelectedKeyQuietly()
        {
            if (this.grid.SelectedRows.Count == 0) return null;
            return Convert.ToString(this.grid.SelectedRows[0].Tag);
        }

        /// <summary>The selected job, or null, without telling the user off.</summary>
        private JobConfig SelectedJobQuietly()
        {
            string key = SelectedKeyQuietly();
            return key == null ? null : _file.FindByKey(key);
        }

        private JobConfig SelectedJob()
        {
            JobConfig job = SelectedJobQuietly();
            if (job != null) return job;

            if (this.grid.Rows.Count > 0)
            {
                MessageBox.Show(this, "Select a job in the Dashboard grid first.", "No job selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return null;
        }

        // ---------------------------------------------------------------- save

        private void btnSave_Click(object sender, EventArgs e)
        {
            JobsFile collected = CollectFromUi();
            JobsFile toSave = collected.WithoutBlankJobs();
            int skipped = collected.Jobs.Count - toSave.Jobs.Count;

            if (toSave.Jobs.Count == 0)
            {
                MessageBox.Show(this,
                    "No job has been filled in yet, so there is nothing to write.\r\n\r\n" +
                    "Fill in the source, the NAS root and the folder name of at least one job on " +
                    "the Jobs tab. One job is enough; the others can stay empty.",
                    "Nothing to save", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SetStatus("Nothing was written: no job has been filled in.");
                return;
            }

            List<string> errors = Validation.ValidateFile(toSave);
            ShowPerEditorProblems(collected);
            if (errors.Count > 0)
            {
                ShowValidationErrors(errors);
                return;
            }

            // 1. Show every script before a single byte is written.
            StringBuilder preview = new StringBuilder();
            List<string> paths = new List<string>();
            List<string> contents = new List<string>();

            try
            {
                foreach (JobConfig job in toSave.Jobs)
                {
                    string content = _generator.Render(job, toSave.VoxRoot);
                    string path = job.ScriptPath(toSave.VoxRoot);

                    paths.Add(path);
                    contents.Add(content);

                    preview.AppendLine("==================================================================");
                    preview.AppendLine(" " + path);
                    preview.AppendLine("==================================================================");
                    preview.AppendLine(content.Replace("\r\n", Environment.NewLine));
                    preview.AppendLine();
                }
            }
            catch (Exception ex)
            {
                ShowError("The scripts could not be generated, so nothing was written.", ex);
                return;
            }

            string caption = "These " + paths.Count + " script(s) will be written to " +
                             toSave.VoxRoot + ". Nothing has been written yet." +
                             (skipped > 0
                                 ? "   " + skipped + " empty job(s) are skipped."
                                 : "");

            using (PreviewForm dialog = new PreviewForm(
                "Preview the generated scripts", caption, preview.ToString(), "Write scripts and save"))
            {
                Theme.Apply(dialog);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    SetStatus("Save cancelled. Nothing was written.");
                    return;
                }
            }

            // 2. Write the scripts and the settings file.
            try
            {
                for (int i = 0; i < paths.Count; i++)
                    _generator.WriteScript(paths[i], contents[i]);

                _repository.Save(toSave);
                _file = _repository.Load();
                BindFile();
            }
            catch (ValidationException ex)
            {
                ShowValidationErrors(ex.Errors);
                return;
            }
            catch (Exception ex)
            {
                ShowError("The scripts or the settings file could not be written.", ex);
                return;
            }

            SetStatus("Wrote " + paths.Count + " script(s) and saved " + _repository.Path +
                      (skipped > 0 ? ". " + skipped + " empty job(s) were skipped." : ""));

            // 3. Offer to create or update the scheduled tasks, which needs the password.
            UpdateTasksAfterSave();

            RefreshDashboard();
            RefreshLogView();
        }

        private void UpdateTasksAfterSave()
        {
            int count = _file.Jobs.Count;

            DialogResult answer = MessageBox.Show(this,
                "Scripts saved.\r\n\r\nCreate or update the " + count + " scheduled task(s) now?\r\n\r\n" +
                "Each task runs every 3 weeks on its Sunday, at the time set for that job, whether " +
                "you are logged on or not, so Windows needs your password once to store the task.",
                "Update scheduled tasks?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (answer != DialogResult.Yes)
            {
                SetStatus("Scripts saved. Scheduled tasks were not changed.");
                return;
            }

            if (!Elevation.IsElevated())
            {
                MessageBox.Show(this,
                    "Creating a task that runs while you are logged off needs administrator rights.\r\n\r\n" +
                    "Use \"Restart as administrator\" at the top of the window, then Save again.",
                    "Administrator rights needed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string userName;
            string password;

            using (PasswordForm dialog = new PasswordForm(Elevation.CurrentUserName(),
                "Windows needs the password of the account the backup tasks will run as."))
            {
                Theme.Apply(dialog);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    SetStatus("Scripts saved. Scheduled tasks were not changed.");
                    return;
                }

                userName = dialog.UserName;
                password = dialog.Password;
                dialog.ClearPassword();
            }

            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show(this,
                    "No password was entered, so the tasks were not changed. A blank password " +
                    "cannot be used for a task that runs while logged off.",
                    "Nothing changed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int done = 0;
            List<string> failures = new List<string>();

            try
            {
                foreach (JobConfig job in _file.Jobs)
                {
                    try
                    {
                        _tasks.CreateOrUpdate(job, _file.VoxRoot, userName, password);
                        done++;
                    }
                    catch (Exception ex)
                    {
                        AppLog.Error("Could not register the task for " + job.Key, ex);
                        failures.Add(job.Key + ": " + ex.Message);
                    }
                }
            }
            finally
            {
                // Drop the only copy of the password this process held.
                password = null;
            }

            if (failures.Count == 0)
            {
                SetStatus("Saved, and " + done + " scheduled task(s) were created or updated.");
            }
            else
            {
                StringBuilder message = new StringBuilder();
                message.AppendLine(done + " task(s) updated, " + failures.Count + " failed:");
                message.AppendLine();
                foreach (string failure in failures)
                    message.AppendLine("  - " + failure);
                message.AppendLine();
                message.AppendLine("Full details are in " + AppPaths.AppLogPath);

                MessageBox.Show(this, message.ToString(), "Some tasks could not be saved",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetStatus("Saved, with " + failures.Count + " task error(s).");
            }
        }

        private void ShowPerEditorProblems(JobsFile collected)
        {
            foreach (JobEditorControl editor in _editors)
            {
                JobConfig job = collected.FindByKey(editor.JobKey);
                if (job == null) continue;

                // An empty job is skipped, not broken, so it is not marked with problems.
                editor.ShowProblems(job.IsBlank ? null : Validation.ValidateJob(job));
            }
        }

        // ---------------------------------------------------------------- progress

        /// <summary>Raised on the progress timer's thread, so the work is handed to the UI thread.</summary>
        private void OnProgressChanged(object sender, ProgressEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;

            try
            {
                BeginInvoke(new Action(delegate { ApplyProgress(e.Progress); }));
            }
            catch (InvalidOperationException)
            {
                // The window closed between the check and the call. This also covers
                // ObjectDisposedException, which is a kind of InvalidOperationException.
            }
        }

        private void ApplyProgress(CopyProgress progress)
        {
            if (IsDisposed) return;

            DataGridViewRow row = FindRow(progress.Key);
            if (row != null)
            {
                row.Cells["Progress"].Value = progress;
                row.Cells["Progress"].ToolTipText = progress.Detail;
                this.grid.InvalidateRow(row.Index);
            }

            UpdateProgressDetail();
            UpdateRunButtons();

            if (!progress.IsActive)
            {
                // The run is over: the task's result, the next run and the log have all changed.
                SetStatus(progress.Key + (progress.IsDryRun ? " dry run" : "") + ": " + progress.BarText.ToLowerInvariant());
                RefreshDashboard();
                RefreshLogView();
            }
        }

        private DataGridViewRow FindRow(string key)
        {
            foreach (DataGridViewRow row in this.grid.Rows)
            {
                if (string.Equals(Convert.ToString(row.Tag), key, StringComparison.OrdinalIgnoreCase))
                    return row;
            }
            return null;
        }

        /// <summary>
        /// Shows the details of the selected job under the grid. It reads the snapshot the grid
        /// itself is showing, so the bar and the text beneath it can never disagree.
        /// </summary>
        private void UpdateProgressDetail()
        {
            CopyProgress progress = null;
            if (this.grid.SelectedRows.Count > 0)
                progress = this.grid.SelectedRows[0].Cells["Progress"].Value as CopyProgress;

            this.lblProgressDetail.Text = progress != null ? progress.Detail : NoProgressText;
        }

        /// <summary>
        /// Start, Pause/Resume and Stop follow the selected job: only what can actually be done
        /// right now is available.
        /// </summary>
        private void UpdateRunButtons()
        {
            JobConfig job = SelectedJobQuietly();
            CopyProgress progress = job == null ? null : _progress.Latest(job.Key);

            bool active = progress != null && progress.IsActive;
            bool running = progress != null && progress.IsRunning;
            bool paused = progress != null && progress.Phase == CopyPhase.Paused;

            this.btnRunNow.Enabled = job != null && !job.IsBlank && !active;
            this.btnDryRun.Enabled = job != null && !job.IsBlank && !active;
            this.btnPause.Enabled = running;
            this.btnPause.Text = paused ? "Resume" : "Pause";
            this.btnStop.Enabled = active;
        }

        private void grid_SelectionChanged(object sender, EventArgs e)
        {
            UpdateProgressDetail();
            UpdateRunButtons();
        }

        // ---------------------------------------------------------------- start a copy

        private void btnRunNow_Click(object sender, EventArgs e)
        {
            JobConfig job = SelectedJob();
            if (job == null) return;

            if (job.IsBlank)
            {
                MessageBox.Show(this,
                    "This job has not been filled in yet. Set its source, NAS root and folder name " +
                    "on the Jobs tab, then use Save.",
                    "Nothing to copy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!_tasks.Exists(job))
            {
                MessageBox.Show(this,
                    "There is no scheduled task named \"" + job.TaskName + "\" yet.\r\n\r\n" +
                    "Use Save first to write the script and create the task.",
                    "No task yet", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // The task is already running, for example the scheduled run that started while CCJ was
            // closed. Starting it again would be ignored, so show its progress instead.
            TaskRunInfo running = null;
            try
            {
                running = _tasks.GetRunInfo(job);
            }
            catch (Exception ex)
            {
                AppLog.Warn("Could not tell whether " + job.TaskName + " is running: " + ex.Message);
            }

            if (running != null && running.Running)
            {
                _progress.TrackRunning(job, running.StartedAt);
                this.tabs.SelectedTab = this.tabDashboard;
                MessageBox.Show(this,
                    "\"" + job.JobName + "\" is already running, so it was not started again.\r\n\r\n" +
                    "Its progress is shown on the Dashboard.",
                    "Already running", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Worked out while the confirmation is open, so the percentage is ready the moment the
            // copy starts. It is a list-only robocopy pass: read only, it copies nothing.
            Task<PlanResult> plan = _progress.PlanAhead(job);

            using (ConfirmRunForm confirm = new ConfirmRunForm(job, false))
            {
                Theme.Apply(confirm);
                SetStatus("Counting files under " + job.SourcePath + "...");

                // Starts now and fills the dialog in once the count is known, so the window opens
                // immediately even when the source is slow or unreachable.
                FillFileCount(confirm, job.SourcePath);
                FillPlan(confirm, plan);

                if (confirm.ShowDialog(this) != DialogResult.OK)
                {
                    _progress.CancelPlan(job.Key);
                    SetStatus("Run cancelled. Nothing was copied.");
                    return;
                }
            }

            try
            {
                // Tracking starts first, so what master.log already says is not mistaken for this run.
                _progress.TrackTask(job);
                _tasks.RunNow(job);
                SetStatus("Started the task \"" + job.TaskName + "\". Its progress is on the Dashboard.");
            }
            catch (Exception ex)
            {
                _progress.Untrack(job.Key);
                ShowError("The task could not be started.", ex);
                return;
            }

            this.tabs.SelectedTab = this.tabDashboard;
            RefreshDashboard();
        }

        /// <summary>
        /// Hands the list-only result to the confirmation dialog when it arrives. A pass that could
        /// not run is shown as "unknown", never as zero files.
        /// </summary>
        private async void FillPlan(ConfirmRunForm confirm, Task<PlanResult> plan)
        {
            PlanResult result = null;
            try
            {
                result = await plan.ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                AppLog.Warn("The list-only check failed: " + ex.Message);
            }

            if (confirm != null && !confirm.IsDisposed)
                confirm.SetPlan(result);
        }

        /// <summary>
        /// Counts the files in the background and hands the number to the confirmation dialog.
        /// The await resumes on the UI thread, so the dialog is touched from the right thread, and
        /// a count that cannot be taken is shown as "unknown" rather than guessed.
        /// </summary>
        private async void FillFileCount(ConfirmRunForm confirm, string sourcePath)
        {
            long? count = null;
            try
            {
                count = await _connectivity.CountFilesAsync(sourcePath, 20000).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                AppLog.Warn("Could not count the files under " + sourcePath + ": " + ex.Message);
            }

            if (confirm != null && !confirm.IsDisposed)
                confirm.SetFileCount(count);
        }

        // ---------------------------------------------------------------- pause, resume and stop

        private void btnPause_Click(object sender, EventArgs e)
        {
            JobConfig job = SelectedJob();
            if (job == null) return;

            CopyProgress progress = _progress.Latest(job.Key);
            if (progress == null || !progress.IsRunning)
            {
                MessageBox.Show(this,
                    "Nothing is being copied for this job at the moment.\r\n\r\n" +
                    "While the script is checking the shares or waiting before its next attempt " +
                    "there is no copy to pause; Stop ends the run itself.",
                    "Nothing to pause", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool pause = !_progress.IsPaused(job.Key);
            List<RunningCopy> copies = FindCopies(job.Key);
            ControlResult result = pause ? CopyControl.Pause(copies) : CopyControl.Resume(copies);

            if (!result.Ok)
            {
                MessageBox.Show(this,
                    "The copy could not be " + (pause ? "paused" : "resumed") + ".\r\n\r\n" +
                    (result.Error == null ? "No reason was reported." : result.Error) +
                    "\r\n\r\nMore detail is in " + AppPaths.AppLogPath,
                    pause ? "Could not pause" : "Could not resume",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);

                SetStatus("The copy was not " + (pause ? "paused" : "resumed") + ".");
                return;
            }

            _progress.SetPaused(job.Key, pause);
            UpdateRunButtons();

            SetStatus(pause
                ? job.Key + " paused (" + result.Affected + " robocopy process(es) suspended). " +
                  "A long pause can drop the connection to the server, which makes the script retry."
                : job.Key + " resumed.");
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            JobConfig job = SelectedJob();
            if (job == null) return;

            CopyProgress progress = _progress.Latest(job.Key);
            if (progress == null || !progress.IsActive)
            {
                MessageBox.Show(this, "This job is not running at the moment.", "Nothing to stop",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool dryRun = progress.IsDryRun;

            DialogResult answer = MessageBox.Show(this,
                "Stop the " + (dryRun ? "dry run" : "backup") + " of \"" + job.JobName + "\" now?\r\n\r\n" +
                "Files that have already been copied stay on the destination; nothing is deleted. " +
                "The file being copied right now is left part written and will be copied again on " +
                "the next run, because robocopy compares sizes and timestamps.\r\n\r\n" +
                "The run will be reported as stopped, not as a success.",
                "Stop the copy?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (answer != DialogResult.Yes) return;

            StringBuilder outcome = new StringBuilder();

            // The script, not just robocopy: stopping robocopy on its own would only make the
            // script wait and try again.
            if (dryRun)
            {
                outcome.Append(StopDryRunProcess());
            }
            else
            {
                try
                {
                    outcome.Append(_tasks.Stop(job)
                        ? "The scheduled task was stopped."
                        : "There was no running task to stop.");
                }
                catch (Exception ex)
                {
                    AppLog.Error("Could not stop the task for " + job.Key, ex);
                    outcome.Append("The task could not be stopped: ").Append(ex.Message);
                }
            }

            // robocopy can outlive the script that started it, so it is ended as well.
            ControlResult killed = CopyControl.Terminate(FindCopies(job.Key));
            if (killed.Affected > 0)
                outcome.Append(" ").Append(killed.Affected).Append(" robocopy process(es) ended.");

            _progress.SetPaused(job.Key, false);
            _progress.DiscoverNow();

            SetStatus(job.Key + " stopped. " + outcome.ToString());
        }

        /// <summary>Ends the dry run CCJ started itself, with its robocopy child.</summary>
        private string StopDryRunProcess()
        {
            Process process = _dryRunProcess;
            if (process == null) return "The dry run had already finished.";

            try
            {
                if (process.HasExited) return "The dry run had already finished.";

                process.Kill();
                return "The dry run script was ended.";
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not end the dry run process.", ex);
                return "The dry run could not be ended: " + ex.Message;
            }
        }

        /// <summary>
        /// The robocopy processes of one job's current run, found by the log file in their command
        /// line so a dry run and a real run of the same job are never mixed up.
        /// </summary>
        private List<RunningCopy> FindCopies(string key)
        {
            string logPath = _progress.ActiveLogPath(key);
            string logsDir = _progress.ActiveLogsDir(key);
            string folder = _progress.ActiveFolder(key);
            return CopyControl.Find(logPath, logsDir, folder);
        }

        // ---------------------------------------------------------------- dry run

        private async void btnDryRun_Click(object sender, EventArgs e)
        {
            JobsFile collected = CollectFromUi();
            JobConfig selected = SelectedJob();
            JobConfig job = selected == null ? null : collected.FindByKey(selected.Key);
            if (job == null) return;

            List<string> errors = Validation.ValidateJob(job);
            if (errors.Count > 0)
            {
                ShowValidationErrors(errors);
                return;
            }

            string dryRunDir = AppPaths.DryRunDir;
            string scriptPath = Path.Combine(dryRunDir, "Backup_" + job.Key + "_DRYRUN.bat");
            string content;

            try
            {
                content = _generator.RenderDryRun(job, dryRunDir);
            }
            catch (Exception ex)
            {
                ShowError("The dry run script could not be generated.", ex);
                return;
            }

            using (PreviewForm dialog = new PreviewForm("Dry run script",
                "robocopy runs with /L, so this can only list files. It never copies or deletes. " +
                "The script and its logs stay in " + dryRunDir + ".",
                content.Replace("\r\n", Environment.NewLine), "Write and run the dry run"))
            {
                Theme.Apply(dialog);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    SetStatus("Dry run cancelled.");
                    return;
                }
            }

            using (ConfirmRunForm confirm = new ConfirmRunForm(job, true))
            {
                Theme.Apply(confirm);
                FillFileCount(confirm, job.SourcePath);

                if (confirm.ShowDialog(this) != DialogResult.OK)
                {
                    SetStatus("Dry run cancelled.");
                    return;
                }
            }

            try
            {
                if (!Directory.Exists(dryRunDir)) Directory.CreateDirectory(dryRunDir);
                _generator.WriteScript(scriptPath, content);

                SetStatus("Dry run started: " + scriptPath);

                ProcessStartInfo info = new ProcessStartInfo("cmd.exe", "/c \"" + scriptPath + "\"");
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.WorkingDirectory = dryRunDir;

                // The progress bar follows the dry run the same way as a real one. It is told to
                // start looking before the script does, and asks the process whether it is alive.
                Process[] holder = new Process[1];
                _progress.TrackProcess(job, dryRunDir,
                    delegate { return holder[0] != null && !holder[0].HasExited; });
                this.tabs.SelectedTab = this.tabDashboard;

                Process started = Process.Start(info);
                holder[0] = started;
                _dryRunProcess = started;
                UpdateRunButtons();

                int exitCode = await Task.Run(new Func<int>(delegate
                {
                    started.WaitForExit();
                    return started.ExitCode;
                })).ConfigureAwait(true);

                _dryRunProcess = null;

                string master = Path.Combine(dryRunDir, "master.log");
                string tail = "";
                try
                {
                    string[] lines = _logs.ReadLastLines(master, 20);
                    tail = string.Join(Environment.NewLine, lines);
                }
                catch (Exception ex)
                {
                    AppLog.Warn("Could not read the dry run log: " + ex.Message);
                }

                MessageBox.Show(this,
                    "Dry run finished with exit code " + exitCode + " (0 means the script reported " +
                    "SUCCESS).\r\n\r\nNothing was copied: robocopy ran with /L.\r\n\r\n" +
                    "Log folder: " + dryRunDir + "\r\n\r\nLast lines:\r\n" + tail,
                    "Dry run finished", MessageBoxButtons.OK,
                    exitCode == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

                SetStatus("Dry run finished with exit code " + exitCode + ". Nothing was copied.");
            }
            catch (Exception ex)
            {
                _dryRunProcess = null;
                _progress.Untrack(job.Key);
                ShowError("The dry run could not be completed.", ex);
            }
        }

        // ---------------------------------------------------------------- checks

        private async void btnConnectivity_Click(object sender, EventArgs e)
        {
            JobsFile collected = CollectFromUi().WithoutBlankJobs();
            if (collected.Jobs.Count == 0)
            {
                MessageBox.Show(this, "No job has been filled in yet, so there is nothing to check.",
                    "Nothing to check", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SetBusy(true);
            SetStatus("Checking the source and the NAS, 3 second timeout per check...");

            List<CheckResult> results;
            try
            {
                results = await _connectivity
                    .CheckJobsAsync(collected, ConnectivityChecker.DefaultTimeoutMs)
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                SetBusy(false);
                ShowError("The connectivity check failed.", ex);
                return;
            }
            SetBusy(false);

            using (TextReportForm report = new TextReportForm("Connectivity",
                "Each path must exist, and each host must answer on TCP 445 within 3 seconds.",
                results, null, null))
            {
                Theme.Apply(report);
                report.ShowDialog(this);
            }
            SetStatus("Connectivity check finished.");
        }

        private void btnCredentials_Click(object sender, EventArgs e)
        {
            JobsFile collected = CollectFromUi().WithoutBlankJobs();
            List<CheckResult> results;

            try
            {
                results = _credentials.CheckHosts(collected);
            }
            catch (Exception ex)
            {
                ShowError("Credential Manager could not be read.", ex);
                return;
            }

            using (TextReportForm report = new TextReportForm("Saved credentials",
                "Whether Windows Credential Manager holds an entry for each host. " +
                "CCJ never reads or shows a password.",
                results, "Open Credential Manager", new EventHandler(OpenCredentialManager)))
            {
                Theme.Apply(report);
                report.ShowDialog(this);
            }
            SetStatus("Credential check finished.");
        }

        private void OpenCredentialManager(object sender, EventArgs e)
        {
            try
            {
                CredentialChecker.OpenCredentialManager();
            }
            catch (Exception ex)
            {
                ShowError("Credential Manager could not be opened.", ex);
            }
        }

        // ---------------------------------------------------------------- logs

        private void RefreshLogView()
        {
            string master = CollectFromUi().MasterLogPath;
            this.lblLogPath.Text = master;

            try
            {
                string[] filter = new string[0];
                if (this.cmbLogFilter.SelectedIndex > 0 &&
                    this.cmbLogFilter.SelectedIndex <= _file.Jobs.Count)
                {
                    JobConfig job = _file.Jobs[this.cmbLogFilter.SelectedIndex - 1];
                    filter = new string[] { job.Key, job.NasFolder, job.JobName };
                }

                string[] lines = _logs.ReadLastLines(master, LogReader.DefaultLineCount, filter);

                if (lines.Length == 0)
                {
                    this.txtLog.Text = File.Exists(master)
                        ? "No matching lines in " + master
                        : "No log file yet at " + master;
                }
                else
                {
                    this.txtLog.Text = string.Join(Environment.NewLine, lines);
                    this.txtLog.SelectionStart = this.txtLog.TextLength;
                    this.txtLog.ScrollToCaret();
                }
            }
            catch (Exception ex)
            {
                this.txtLog.Text = "Could not read " + master + Environment.NewLine + ex.Message;
                AppLog.Error("Could not read the master log.", ex);
            }
        }

        private void btnRefreshLog_Click(object sender, EventArgs e)
        {
            RefreshLogView();
        }

        private void cmbLogFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            RefreshLogView();
        }

        // ---------------------------------------------------------------- appearance

        private void chkDarkMode_CheckedChanged(object sender, EventArgs e)
        {
            if (_loading) return;

            _ui.DarkMode = this.chkDarkMode.Checked;
            Theme.Use(_ui.DarkMode);
            ApplyTheme();

            if (!_ui.Save())
                AppLog.Warn("The dark mode setting could not be written to " + AppPaths.UiSettingsPath);

            // The grid holds colours of its own for missing tasks and failed runs.
            if (_file != null) RefreshDashboard();

            SetStatus(_ui.DarkMode ? "Dark mode on." : "Dark mode off.");
        }

        private void ApplyTheme()
        {
            Theme.Apply(this);
            this.tabs.Invalidate();
            this.grid.Invalidate();
        }

        private void tabs_DrawItem(object sender, DrawItemEventArgs e)
        {
            Theme.DrawTab(this.tabs, e);
        }

        // ---------------------------------------------------------------- misc

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            _progress.DiscoverNow();
            RefreshDashboard();
            RefreshLogView();
            SetStatus("Refreshed.");
        }

        private void btnElevate_Click(object sender, EventArgs e)
        {
            if (Elevation.RestartElevated())
                Application.Exit();
            else
                SetStatus("Not restarted. Administrator rights were not granted.");
        }

        private void btnBrowseVoxRoot_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Folder for the generated Backup_<job>.bat files and the Logs folder.";
                dialog.SelectedPath = Directory.Exists(this.txtVoxRoot.Text)
                    ? this.txtVoxRoot.Text
                    : AppPaths.ExeDir;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                    this.txtVoxRoot.Text = dialog.SelectedPath;
            }
        }

        private void SetStatus(string message)
        {
            this.statusLabel.Text = message;
        }

        private void SetBusy(bool busy)
        {
            this.Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            this.pnlButtons.Enabled = !busy;
        }

        private void ShowValidationErrors(List<string> errors)
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("Nothing was written. Please fix these first:");
            message.AppendLine();
            foreach (string error in errors)
                message.AppendLine("  - " + error);

            MessageBox.Show(this, message.ToString(), "Check the job settings",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);

            SetStatus(errors.Count + " problem(s) found. Nothing was written.");
        }

        private void ShowError(string summary, Exception ex)
        {
            AppLog.Error(summary, ex);

            string detail = ex == null ? "" : ex.Message;
            MessageBox.Show(this,
                summary + "\r\n\r\n" + detail + "\r\n\r\nThe full details are in:\r\n" +
                AppPaths.AppLogPath,
                "CCJ", MessageBoxButtons.OK, MessageBoxIcon.Error);

            SetStatus(summary);
        }
    }
}
