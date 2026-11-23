namespace CCJ.Forms
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Panel pnlElevation;
        private System.Windows.Forms.Label lblElevation;
        private System.Windows.Forms.Button btnElevate;

        private System.Windows.Forms.TabControl tabs;
        private System.Windows.Forms.TabPage tabDashboard;
        private System.Windows.Forms.TabPage tabJobs;
        private System.Windows.Forms.TabPage tabLogs;

        private System.Windows.Forms.DataGridView grid;
        private System.Windows.Forms.Label lblProgressDetail;

        private System.Windows.Forms.Panel pnlJobsHeader;
        private System.Windows.Forms.Label lblVoxRoot;
        private System.Windows.Forms.TextBox txtVoxRoot;
        private System.Windows.Forms.Button btnBrowseVoxRoot;
        private System.Windows.Forms.Label lblVoxRootHint;
        private System.Windows.Forms.Button btnAddJob;
        private System.Windows.Forms.Label lblJobsHint;
        private System.Windows.Forms.Panel pnlJobEditors;

        private System.Windows.Forms.Panel pnlLogToolbar;
        private System.Windows.Forms.Label lblFilter;
        private System.Windows.Forms.ComboBox cmbLogFilter;
        private System.Windows.Forms.Button btnRefreshLog;
        private System.Windows.Forms.Label lblLogPath;
        private System.Windows.Forms.TextBox txtLog;

        private System.Windows.Forms.Panel pnlButtons;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnRunNow;
        private System.Windows.Forms.Button btnPause;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Button btnDryRun;
        private System.Windows.Forms.Button btnConnectivity;
        private System.Windows.Forms.Button btnCredentials;
        private System.Windows.Forms.CheckBox chkDarkMode;
        private System.Windows.Forms.Button btnRefresh;

        private System.Windows.Forms.StatusStrip status;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.pnlElevation = new System.Windows.Forms.Panel();
            this.lblElevation = new System.Windows.Forms.Label();
            this.btnElevate = new System.Windows.Forms.Button();
            this.tabs = new System.Windows.Forms.TabControl();
            this.tabDashboard = new System.Windows.Forms.TabPage();
            this.tabJobs = new System.Windows.Forms.TabPage();
            this.tabLogs = new System.Windows.Forms.TabPage();
            this.grid = new System.Windows.Forms.DataGridView();
            this.lblProgressDetail = new System.Windows.Forms.Label();
            this.pnlJobsHeader = new System.Windows.Forms.Panel();
            this.lblVoxRoot = new System.Windows.Forms.Label();
            this.txtVoxRoot = new System.Windows.Forms.TextBox();
            this.btnBrowseVoxRoot = new System.Windows.Forms.Button();
            this.lblVoxRootHint = new System.Windows.Forms.Label();
            this.btnAddJob = new System.Windows.Forms.Button();
            this.lblJobsHint = new System.Windows.Forms.Label();
            this.pnlJobEditors = new System.Windows.Forms.Panel();
            this.pnlLogToolbar = new System.Windows.Forms.Panel();
            this.lblFilter = new System.Windows.Forms.Label();
            this.cmbLogFilter = new System.Windows.Forms.ComboBox();
            this.btnRefreshLog = new System.Windows.Forms.Button();
            this.lblLogPath = new System.Windows.Forms.Label();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.pnlButtons = new System.Windows.Forms.Panel();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnRunNow = new System.Windows.Forms.Button();
            this.btnPause = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.btnDryRun = new System.Windows.Forms.Button();
            this.btnConnectivity = new System.Windows.Forms.Button();
            this.btnCredentials = new System.Windows.Forms.Button();
            this.chkDarkMode = new System.Windows.Forms.CheckBox();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.status = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.SuspendLayout();
            //
            // pnlElevation
            //
            this.pnlElevation.BackColor = System.Drawing.Color.FromArgb(255, 248, 220);
            this.pnlElevation.Tag = "banner";
            this.pnlElevation.Controls.Add(this.lblElevation);
            this.pnlElevation.Controls.Add(this.btnElevate);
            this.pnlElevation.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlElevation.Location = new System.Drawing.Point(0, 0);
            this.pnlElevation.Name = "pnlElevation";
            this.pnlElevation.Size = new System.Drawing.Size(1060, 42);
            this.pnlElevation.TabIndex = 0;
            this.pnlElevation.Visible = false;
            //
            // lblElevation
            //
            this.lblElevation.AutoSize = true;
            this.lblElevation.Location = new System.Drawing.Point(14, 13);
            this.lblElevation.Name = "lblElevation";
            this.lblElevation.Size = new System.Drawing.Size(600, 15);
            this.lblElevation.TabIndex = 0;
            this.lblElevation.Text = "Not running as administrator. Creating or changing a scheduled task needs administrator rights.";
            //
            // btnElevate
            //
            this.btnElevate.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top
                | System.Windows.Forms.AnchorStyles.Right));
            this.btnElevate.Location = new System.Drawing.Point(840, 8);
            this.btnElevate.Name = "btnElevate";
            this.btnElevate.Size = new System.Drawing.Size(200, 27);
            this.btnElevate.TabIndex = 1;
            this.btnElevate.Text = "Restart as administrator";
            this.btnElevate.UseVisualStyleBackColor = true;
            this.btnElevate.Click += new System.EventHandler(this.btnElevate_Click);
            //
            // tabs
            //
            this.tabs.Controls.Add(this.tabDashboard);
            this.tabs.Controls.Add(this.tabJobs);
            this.tabs.Controls.Add(this.tabLogs);
            this.tabs.DrawMode = System.Windows.Forms.TabDrawMode.OwnerDrawFixed;
            this.tabs.ItemSize = new System.Drawing.Size(120, 26);
            this.tabs.SizeMode = System.Windows.Forms.TabSizeMode.Fixed;
            this.tabs.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.tabs_DrawItem);
            this.tabs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabs.Location = new System.Drawing.Point(0, 42);
            this.tabs.Name = "tabs";
            this.tabs.Padding = new System.Drawing.Point(12, 4);
            this.tabs.SelectedIndex = 0;
            this.tabs.Size = new System.Drawing.Size(1060, 530);
            this.tabs.TabIndex = 1;
            //
            // tabDashboard
            //
            this.tabDashboard.Controls.Add(this.grid);
            this.tabDashboard.Controls.Add(this.lblProgressDetail);
            this.tabDashboard.Location = new System.Drawing.Point(4, 27);
            this.tabDashboard.Name = "tabDashboard";
            this.tabDashboard.Padding = new System.Windows.Forms.Padding(10);
            this.tabDashboard.Size = new System.Drawing.Size(1052, 499);
            this.tabDashboard.TabIndex = 0;
            this.tabDashboard.Text = "Dashboard";
            this.tabDashboard.UseVisualStyleBackColor = true;
            //
            // grid
            //
            this.grid.AllowUserToAddRows = false;
            this.grid.AllowUserToDeleteRows = false;
            this.grid.AllowUserToResizeRows = false;
            this.grid.BackgroundColor = System.Drawing.Color.White;
            this.grid.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.grid.ColumnHeadersHeightSizeMode =
                System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.grid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grid.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.grid.Location = new System.Drawing.Point(10, 10);
            this.grid.MultiSelect = false;
            this.grid.Name = "grid";
            this.grid.ReadOnly = true;
            this.grid.RowHeadersVisible = false;
            this.grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.grid.Size = new System.Drawing.Size(1032, 395);
            this.grid.TabIndex = 0;
            this.grid.SelectionChanged += new System.EventHandler(this.grid_SelectionChanged);
            //
            // lblProgressDetail
            //
            this.lblProgressDetail.BackColor = System.Drawing.Color.FromArgb(244, 247, 251);
            this.lblProgressDetail.Tag = "surface";
            this.lblProgressDetail.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblProgressDetail.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.lblProgressDetail.Location = new System.Drawing.Point(10, 405);
            this.lblProgressDetail.Name = "lblProgressDetail";
            this.lblProgressDetail.Padding = new System.Windows.Forms.Padding(8, 6, 8, 6);
            this.lblProgressDetail.Size = new System.Drawing.Size(1032, 84);
            this.lblProgressDetail.TabIndex = 1;
            this.lblProgressDetail.Text = "";
            //
            // tabJobs
            //
            this.tabJobs.Controls.Add(this.pnlJobEditors);
            this.tabJobs.Controls.Add(this.pnlJobsHeader);
            this.tabJobs.Location = new System.Drawing.Point(4, 27);
            this.tabJobs.Name = "tabJobs";
            this.tabJobs.Padding = new System.Windows.Forms.Padding(10);
            this.tabJobs.Size = new System.Drawing.Size(1052, 499);
            this.tabJobs.TabIndex = 1;
            this.tabJobs.Text = "Jobs";
            this.tabJobs.UseVisualStyleBackColor = true;
            //
            // pnlJobsHeader
            //
            this.pnlJobsHeader.Controls.Add(this.lblVoxRoot);
            this.pnlJobsHeader.Controls.Add(this.txtVoxRoot);
            this.pnlJobsHeader.Controls.Add(this.btnBrowseVoxRoot);
            this.pnlJobsHeader.Controls.Add(this.lblVoxRootHint);
            this.pnlJobsHeader.Controls.Add(this.btnAddJob);
            this.pnlJobsHeader.Controls.Add(this.lblJobsHint);
            this.pnlJobsHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlJobsHeader.Location = new System.Drawing.Point(10, 10);
            this.pnlJobsHeader.Name = "pnlJobsHeader";
            this.pnlJobsHeader.Size = new System.Drawing.Size(1032, 100);
            this.pnlJobsHeader.TabIndex = 0;
            //
            // lblVoxRoot
            //
            this.lblVoxRoot.AutoSize = true;
            this.lblVoxRoot.Location = new System.Drawing.Point(6, 11);
            this.lblVoxRoot.Name = "lblVoxRoot";
            this.lblVoxRoot.Size = new System.Drawing.Size(120, 15);
            this.lblVoxRoot.TabIndex = 0;
            this.lblVoxRoot.Text = "Script and log folder";
            //
            // txtVoxRoot
            //
            this.txtVoxRoot.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top
                | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right));
            this.txtVoxRoot.Location = new System.Drawing.Point(160, 8);
            this.txtVoxRoot.Name = "txtVoxRoot";
            this.txtVoxRoot.Size = new System.Drawing.Size(740, 23);
            this.txtVoxRoot.TabIndex = 1;
            //
            // btnBrowseVoxRoot
            //
            this.btnBrowseVoxRoot.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top
                | System.Windows.Forms.AnchorStyles.Right));
            this.btnBrowseVoxRoot.Location = new System.Drawing.Point(910, 7);
            this.btnBrowseVoxRoot.Name = "btnBrowseVoxRoot";
            this.btnBrowseVoxRoot.Size = new System.Drawing.Size(110, 25);
            this.btnBrowseVoxRoot.TabIndex = 2;
            this.btnBrowseVoxRoot.Text = "Browse...";
            this.btnBrowseVoxRoot.UseVisualStyleBackColor = true;
            this.btnBrowseVoxRoot.Click += new System.EventHandler(this.btnBrowseVoxRoot_Click);
            //
            // lblVoxRootHint
            //
            this.lblVoxRootHint.AutoSize = true;
            this.lblVoxRootHint.ForeColor = System.Drawing.Color.DimGray;
            this.lblVoxRootHint.Tag = "hint";
            this.lblVoxRootHint.Location = new System.Drawing.Point(160, 37);
            this.lblVoxRootHint.Name = "lblVoxRootHint";
            this.lblVoxRootHint.Size = new System.Drawing.Size(700, 15);
            this.lblVoxRootHint.TabIndex = 3;
            this.lblVoxRootHint.Text = "One Backup_<job>.bat per job is written here, and logs go to its Logs subfolder.";
            //
            // btnAddJob
            //
            this.btnAddJob.Location = new System.Drawing.Point(6, 64);
            this.btnAddJob.Name = "btnAddJob";
            this.btnAddJob.Size = new System.Drawing.Size(130, 27);
            this.btnAddJob.TabIndex = 4;
            this.btnAddJob.Text = "Add another job";
            this.btnAddJob.UseVisualStyleBackColor = true;
            this.btnAddJob.Click += new System.EventHandler(this.btnAddJob_Click);
            //
            // lblJobsHint
            //
            this.lblJobsHint.AutoSize = true;
            this.lblJobsHint.ForeColor = System.Drawing.Color.DimGray;
            this.lblJobsHint.Tag = "hint";
            this.lblJobsHint.Location = new System.Drawing.Point(146, 70);
            this.lblJobsHint.Name = "lblJobsHint";
            this.lblJobsHint.Size = new System.Drawing.Size(860, 15);
            this.lblJobsHint.TabIndex = 5;
            this.lblJobsHint.Text = "As many jobs as you need. A job you leave empty is skipped, so one filled in job is enough to save and to copy. Enabled takes effect at once.";
            //
            // pnlJobEditors
            //
            this.pnlJobEditors.AutoScroll = true;
            this.pnlJobEditors.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlJobEditors.Location = new System.Drawing.Point(10, 110);
            this.pnlJobEditors.Name = "pnlJobEditors";
            this.pnlJobEditors.Size = new System.Drawing.Size(1032, 379);
            this.pnlJobEditors.TabIndex = 1;
            //
            // tabLogs
            //
            this.tabLogs.Controls.Add(this.txtLog);
            this.tabLogs.Controls.Add(this.pnlLogToolbar);
            this.tabLogs.Location = new System.Drawing.Point(4, 27);
            this.tabLogs.Name = "tabLogs";
            this.tabLogs.Padding = new System.Windows.Forms.Padding(10);
            this.tabLogs.Size = new System.Drawing.Size(1052, 499);
            this.tabLogs.TabIndex = 2;
            this.tabLogs.Text = "Logs";
            this.tabLogs.UseVisualStyleBackColor = true;
            //
            // pnlLogToolbar
            //
            this.pnlLogToolbar.Controls.Add(this.lblFilter);
            this.pnlLogToolbar.Controls.Add(this.cmbLogFilter);
            this.pnlLogToolbar.Controls.Add(this.btnRefreshLog);
            this.pnlLogToolbar.Controls.Add(this.lblLogPath);
            this.pnlLogToolbar.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlLogToolbar.Location = new System.Drawing.Point(10, 10);
            this.pnlLogToolbar.Name = "pnlLogToolbar";
            this.pnlLogToolbar.Size = new System.Drawing.Size(1032, 64);
            this.pnlLogToolbar.TabIndex = 0;
            //
            // lblFilter
            //
            this.lblFilter.AutoSize = true;
            this.lblFilter.Location = new System.Drawing.Point(6, 12);
            this.lblFilter.Name = "lblFilter";
            this.lblFilter.Size = new System.Drawing.Size(80, 15);
            this.lblFilter.TabIndex = 0;
            this.lblFilter.Text = "Filter by job";
            //
            // cmbLogFilter
            //
            this.cmbLogFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbLogFilter.Location = new System.Drawing.Point(100, 9);
            this.cmbLogFilter.Name = "cmbLogFilter";
            this.cmbLogFilter.Size = new System.Drawing.Size(240, 23);
            this.cmbLogFilter.TabIndex = 1;
            this.cmbLogFilter.SelectedIndexChanged += new System.EventHandler(this.cmbLogFilter_SelectedIndexChanged);
            //
            // btnRefreshLog
            //
            this.btnRefreshLog.Location = new System.Drawing.Point(356, 8);
            this.btnRefreshLog.Name = "btnRefreshLog";
            this.btnRefreshLog.Size = new System.Drawing.Size(110, 25);
            this.btnRefreshLog.TabIndex = 2;
            this.btnRefreshLog.Text = "Refresh";
            this.btnRefreshLog.UseVisualStyleBackColor = true;
            this.btnRefreshLog.Click += new System.EventHandler(this.btnRefreshLog_Click);
            //
            // lblLogPath
            //
            this.lblLogPath.AutoEllipsis = true;
            this.lblLogPath.ForeColor = System.Drawing.Color.DimGray;
            this.lblLogPath.Tag = "hint";
            this.lblLogPath.Location = new System.Drawing.Point(6, 40);
            this.lblLogPath.Name = "lblLogPath";
            this.lblLogPath.Size = new System.Drawing.Size(1020, 15);
            this.lblLogPath.TabIndex = 3;
            this.lblLogPath.Text = "-";
            //
            // txtLog
            //
            this.txtLog.BackColor = System.Drawing.Color.White;
            this.txtLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLog.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtLog.Location = new System.Drawing.Point(10, 74);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtLog.Size = new System.Drawing.Size(1032, 415);
            this.txtLog.TabIndex = 1;
            this.txtLog.WordWrap = false;
            //
            // pnlButtons
            //
            this.pnlButtons.Controls.Add(this.btnSave);
            this.pnlButtons.Controls.Add(this.btnRunNow);
            this.pnlButtons.Controls.Add(this.btnPause);
            this.pnlButtons.Controls.Add(this.btnStop);
            this.pnlButtons.Controls.Add(this.btnDryRun);
            this.pnlButtons.Controls.Add(this.btnConnectivity);
            this.pnlButtons.Controls.Add(this.btnCredentials);
            this.pnlButtons.Controls.Add(this.chkDarkMode);
            this.pnlButtons.Controls.Add(this.btnRefresh);
            this.pnlButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlButtons.Location = new System.Drawing.Point(0, 572);
            this.pnlButtons.Name = "pnlButtons";
            this.pnlButtons.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
            this.pnlButtons.Size = new System.Drawing.Size(1060, 54);
            this.pnlButtons.TabIndex = 2;
            //
            // btnSave
            //
            this.btnSave.Location = new System.Drawing.Point(12, 10);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(140, 32);
            this.btnSave.TabIndex = 0;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            //
            // btnRunNow
            //
            this.btnRunNow.Location = new System.Drawing.Point(158, 10);
            this.btnRunNow.Name = "btnRunNow";
            this.btnRunNow.Size = new System.Drawing.Size(110, 32);
            this.btnRunNow.TabIndex = 1;
            this.btnRunNow.Text = "Start copy";
            this.btnRunNow.UseVisualStyleBackColor = true;
            this.btnRunNow.Click += new System.EventHandler(this.btnRunNow_Click);
            //
            // btnPause
            //
            this.btnPause.Location = new System.Drawing.Point(274, 10);
            this.btnPause.Name = "btnPause";
            this.btnPause.Size = new System.Drawing.Size(100, 32);
            this.btnPause.TabIndex = 2;
            this.btnPause.Text = "Pause";
            this.btnPause.UseVisualStyleBackColor = true;
            this.btnPause.Click += new System.EventHandler(this.btnPause_Click);
            //
            // btnStop
            //
            this.btnStop.Location = new System.Drawing.Point(380, 10);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(90, 32);
            this.btnStop.TabIndex = 3;
            this.btnStop.Text = "Stop";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            //
            // btnDryRun
            //
            this.btnDryRun.Location = new System.Drawing.Point(476, 10);
            this.btnDryRun.Name = "btnDryRun";
            this.btnDryRun.Size = new System.Drawing.Size(120, 32);
            this.btnDryRun.TabIndex = 4;
            this.btnDryRun.Text = "Test (dry run)";
            this.btnDryRun.UseVisualStyleBackColor = true;
            this.btnDryRun.Click += new System.EventHandler(this.btnDryRun_Click);
            //
            // btnConnectivity
            //
            this.btnConnectivity.Location = new System.Drawing.Point(610, 10);
            this.btnConnectivity.Name = "btnConnectivity";
            this.btnConnectivity.Size = new System.Drawing.Size(130, 32);
            this.btnConnectivity.TabIndex = 5;
            this.btnConnectivity.Text = "Check connectivity";
            this.btnConnectivity.UseVisualStyleBackColor = true;
            this.btnConnectivity.Click += new System.EventHandler(this.btnConnectivity_Click);
            //
            // btnCredentials
            //
            this.btnCredentials.Location = new System.Drawing.Point(746, 10);
            this.btnCredentials.Name = "btnCredentials";
            this.btnCredentials.Size = new System.Drawing.Size(130, 32);
            this.btnCredentials.TabIndex = 6;
            this.btnCredentials.Text = "Check credentials";
            this.btnCredentials.UseVisualStyleBackColor = true;
            this.btnCredentials.Click += new System.EventHandler(this.btnCredentials_Click);
            //
            // chkDarkMode
            //
            this.chkDarkMode.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top
                | System.Windows.Forms.AnchorStyles.Right));
            this.chkDarkMode.AutoSize = true;
            this.chkDarkMode.Location = new System.Drawing.Point(910, 18);
            this.chkDarkMode.Name = "chkDarkMode";
            this.chkDarkMode.Size = new System.Drawing.Size(90, 19);
            this.chkDarkMode.TabIndex = 7;
            this.chkDarkMode.Text = "Dark mode";
            this.chkDarkMode.UseVisualStyleBackColor = true;
            this.chkDarkMode.CheckedChanged += new System.EventHandler(this.chkDarkMode_CheckedChanged);
            //
            // btnRefresh
            //
            this.btnRefresh.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top
                | System.Windows.Forms.AnchorStyles.Right));
            this.btnRefresh.Location = new System.Drawing.Point(1020, 10);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(28, 32);
            this.btnRefresh.TabIndex = 8;
            this.btnRefresh.Tag = "refresh";
            this.btnRefresh.Text = "↻";
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            //
            // status
            //
            this.status.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.statusLabel });
            this.status.Location = new System.Drawing.Point(0, 626);
            this.status.Name = "status";
            this.status.Size = new System.Drawing.Size(1060, 22);
            this.status.TabIndex = 3;
            //
            // statusLabel
            //
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(39, 17);
            this.statusLabel.Text = "Ready";
            //
            // MainForm
            //
            this.ClientSize = new System.Drawing.Size(1180, 720);
            this.Controls.Add(this.tabs);
            this.Controls.Add(this.pnlElevation);
            this.Controls.Add(this.pnlButtons);
            this.Controls.Add(this.status);
            this.MinimumSize = new System.Drawing.Size(900, 560);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "CCJ - Robocopy backup jobs";
            this.ResumeLayout(false);
        }
    }
}
