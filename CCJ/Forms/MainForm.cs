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
        private JobsFile _file;
        private bool _loading;

        public MainForm()
        {
            InitializeComponent();
            BuildJobEditors();
            BuildGridColumns();

            // Follows running backups on a timer of its own. The form only receives the results.
            _progress = new ProgressService(
                delegate { return _file; },
                delegate(JobConfig job) { return _tasks.GetRunInfo(job); });
            _progress.Changed += new EventHandler<ProgressEventArgs>(OnProgressChanged);

            this.lblProgressDetail.Text = NoProgressText;
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
            SetStatus("Loaded " + _file.Jobs.Count + " jobs from " + _repository.Path);

            // Every 1.5 seconds; it also looks for a task that is already running.
            _progress.Start(1500);
        }

        // ---------------------------------------------------------------- setup

        private void BuildJobEditors()
        {
            // Stacked top down inside a scrolling panel, so three fixed jobs always fit.
            for (int i = 2; i >= 0; i--)
            {
                JobEditorControl editor = new JobEditorControl();
                editor.Dock = DockStyle.Top;
                editor.Height = 212;
                editor.ValueChanged += new EventHandler(OnEditorValueChanged);
                this.pnlJobEditors.Controls.Add(editor);
                _editors.Insert(0, editor);
            }
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

                for (int i = 0; i < _editors.Count && i < _file.Jobs.Count; i++)
                    _editors[i].Bind(_file.Jobs[i]);

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

            for (int i = 0; i < _editors.Count; i++)
            {
                JobConfig job = i < _file.Jobs.Count ? _file.Jobs[i].Clone() : new JobConfig();
                _editors[i].ApplyTo(job);
                collected.Jobs.Add(job);
            }
            return collected;
        }

        private void OnEditorValueChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            SetStatus("Unsaved changes. Use Save to write the scripts and update the tasks.");
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
                    JobStatus status = _tasks.GetStatus(job);
                    string lastLine = "";
                    try
                    {
                        lastLine = _logs.LastLineFor(shown.MasterLogPath, job.Key, job.NasFolder, job.JobName);
                    }
                    catch (Exception ex)
                    {
                        AppLog.Warn("Could not read master.log: " + ex.Message);
                    }

                    CopyProgress progress = _progress.Latest(job.Key);

                    int index = this.grid.Rows.Add(
                        job.Key,
                        job.SourcePath,
                        job.DestinationPath,
                        job.Enabled ? "yes" : "no",
                        status.TaskExists ? (status.TaskEnabled ? "enabled" : "disabled") : "missing",
                        progress,
                        status.NextRunText,
                        status.LastRunText,
                        status.LastResultText,
                        lastLine);

                    DataGridViewRow row = this.grid.Rows[index];
                    row.Tag = job.Key;
                    if (progress != null) row.Cells["Progress"].ToolTipText = progress.Detail;

                    if (!status.TaskExists)
                        row.Cells["Task"].Style.ForeColor = Color.Firebrick;
                    else if (!status.TaskEnabled)
                        row.Cells["Task"].Style.ForeColor = Color.DarkOrange;

                    if (status.LastTaskResult.HasValue && status.LastTaskResult.Value != 0 &&
                        status.LastTaskResult.Value != 0x41300 && status.LastTaskResult.Value != 0x41303)
                    {
                        row.Cells["LastResult"].Style.ForeColor = Color.Firebrick;
                    }
                }

                SelectRow(selectedKey);
                UpdateProgressDetail();
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

        private JobConfig SelectedJob()
        {
            if (this.grid.SelectedRows.Count > 0)
            {
                string key = Convert.ToString(this.grid.SelectedRows[0].Tag);
                JobConfig job = _file.FindByKey(key);
                if (job != null) return job;
            }

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

            List<string> errors = Validation.ValidateFile(collected);
            if (errors.Count > 0)
            {
                ShowValidationErrors(errors);
                ShowPerEditorProblems(collected);
                return;
            }
            ShowPerEditorProblems(collected);

            // 1. Show every script before a single byte is written.
            StringBuilder preview = new StringBuilder();
            List<string> paths = new List<string>();
            List<string> contents = new List<string>();

            try
            {
                foreach (JobConfig job in collected.Jobs)
                {
                    string content = _generator.Render(job, collected.VoxRoot);
                    string path = job.ScriptPath(collected.VoxRoot);

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

            string caption = "These " + paths.Count + " scripts will be written to " +
                             collected.VoxRoot + ". Nothing has been written yet.";

            using (PreviewForm dialog = new PreviewForm(
                "Preview the generated scripts", caption, preview.ToString(), "Write scripts and save"))
            {
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

                _repository.Save(collected);
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

            SetStatus("Wrote " + paths.Count + " scripts and saved " + _repository.Path);

            // 3. Offer to create or update the scheduled tasks, which needs the password.
            UpdateTasksAfterSave();

            RefreshDashboard();
            RefreshLogView();
        }

        private void UpdateTasksAfterSave()
        {
            DialogResult answer = MessageBox.Show(this,
                "Scripts saved.\r\n\r\nCreate or update the three scheduled tasks now?\r\n\r\n" +
                "Each task runs every 3 weeks on Sunday at 00:00 whether you are logged on or not, " +
                "so Windows needs your password once to store the task.",
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
                SetStatus("Saved, and " + done + " scheduled tasks were created or updated.");
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
            for (int i = 0; i < _editors.Count && i < collected.Jobs.Count; i++)
                _editors[i].ShowProblems(Validation.ValidateJob(collected.Jobs[i]));
        }

        // ---------------------------------------------------------------- preview

        private void btnPreview_Click(object sender, EventArgs e)
        {
            JobsFile collected = CollectFromUi();
            JobConfig job = SelectedJob();

            List<JobConfig> toShow = new List<JobConfig>();
            if (job == null)
                toShow.AddRange(collected.Jobs);
            else
                toShow.Add(collected.FindByKey(job.Key));

            StringBuilder preview = new StringBuilder();
            try
            {
                foreach (JobConfig one in toShow)
                {
                    preview.AppendLine("==================================================================");
                    preview.AppendLine(" " + one.ScriptPath(collected.VoxRoot));
                    preview.AppendLine("==================================================================");
                    preview.AppendLine(_generator.Render(one, collected.VoxRoot)
                        .Replace("\r\n", Environment.NewLine));
                    preview.AppendLine();
                }
            }
            catch (Exception ex)
            {
                ShowError("The script could not be generated.", ex);
                return;
            }

            using (PreviewForm dialog = new PreviewForm("Script preview",
                "Read only preview. Nothing is written until you use Save.",
                preview.ToString(), "Close"))
            {
                dialog.ShowDialog(this);
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

        private void grid_SelectionChanged(object sender, EventArgs e)
        {
            UpdateProgressDetail();
        }

        // ---------------------------------------------------------------- run now

        private void btnRunNow_Click(object sender, EventArgs e)
        {
            JobConfig job = SelectedJob();
            if (job == null) return;

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
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    SetStatus("Dry run cancelled.");
                    return;
                }
            }

            using (ConfirmRunForm confirm = new ConfirmRunForm(job, true))
            {
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
                SetBusy(true);

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

                int exitCode = await Task.Run(new Func<int>(delegate
                {
                    started.WaitForExit();
                    return started.ExitCode;
                })).ConfigureAwait(true);

                SetBusy(false);

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
                SetBusy(false);
                _progress.Untrack(job.Key);
                ShowError("The dry run could not be completed.", ex);
            }
        }

        // ---------------------------------------------------------------- enable / disable

        private void btnToggleEnabled_Click(object sender, EventArgs e)
        {
            JobConfig job = SelectedJob();
            if (job == null) return;

            bool wanted = !job.Enabled;

            try
            {
                job.Enabled = wanted;
                _repository.Save(_file);

                bool taskChanged = _tasks.SetEnabled(job, wanted);

                BindFile();
                RefreshDashboard();

                if (taskChanged)
                {
                    SetStatus(job.Key + " is now " + (wanted ? "enabled" : "disabled") +
                              ", and the scheduled task was updated.");
                }
                else
                {
                    SetStatus(job.Key + " is now " + (wanted ? "enabled" : "disabled") +
                              " in the settings. There is no scheduled task yet, so use Save to create it.");
                }
            }
            catch (ValidationException ex)
            {
                job.Enabled = !wanted;
                ShowValidationErrors(ex.Errors);
            }
            catch (UnauthorizedAccessException ex)
            {
                job.Enabled = !wanted;
                ShowError("Changing the task needs administrator rights. Use \"Restart as administrator\".", ex);
            }
            catch (Exception ex)
            {
                job.Enabled = !wanted;
                ShowError("The job could not be switched.", ex);
            }
        }

        // ---------------------------------------------------------------- checks

        private async void btnConnectivity_Click(object sender, EventArgs e)
        {
            JobsFile collected = CollectFromUi();

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
                report.ShowDialog(this);
            }
            SetStatus("Connectivity check finished.");
        }

        private void btnCredentials_Click(object sender, EventArgs e)
        {
            JobsFile collected = CollectFromUi();
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

        private void btnOpenLogs_Click(object sender, EventArgs e)
        {
            string dir = CollectFromUi().LogsDir;
            try
            {
                if (!Directory.Exists(dir))
                {
                    DialogResult answer = MessageBox.Show(this,
                        dir + " does not exist yet.\r\n\r\nCreate it now?", "No log folder",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (answer != DialogResult.Yes) return;
                    Directory.CreateDirectory(dir);
                }
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception ex)
            {
                ShowError("The log folder could not be opened.", ex);
            }
        }

        private void RefreshLogView()
        {
            string master = CollectFromUi().MasterLogPath;
            this.lblLogPath.Text = master;

            try
            {
                string[] filter = new string[0];
                if (this.cmbLogFilter.SelectedIndex > 0)
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
                dialog.Description = "Folder for the generated Backup_FolderN.bat files and the Logs folder.";
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
