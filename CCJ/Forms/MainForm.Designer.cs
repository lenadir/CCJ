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
        private System.Windows.Forms.Panel pnlJobEditors;

        private System.Windows.Forms.Panel pnlLogToolbar;
        private System.Windows.Forms.Label lblFilter;
        private System.Windows.Forms.ComboBox cmbLogFilter;
        private System.Windows.Forms.Button btnRefreshLog;
        private System.Windows.Forms.Label lblLogPath;
        private System.Windows.Forms.TextBox txtLog;

        private System.Windows.Forms.Panel pnlButtons;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnPreview;
        private System.Windows.Forms.Button btnRunNow;
        private System.Windows.Forms.Button btnDryRun;
        private System.Windows.Forms.Button btnToggleEnabled;
        private System.Windows.Forms.Button btnOpenLogs;
        private System.Windows.Forms.Button btnConnectivity;
        private System.Windows.Forms.Button btnCredentials;
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
            this.pnlJobEditors = new System.Windows.Forms.Panel();
            this.pnlLogToolbar = new System.Windows.Forms.Panel();
            this.lblFilter = new System.Windows.Forms.Label();
            this.cmbLogFilter = new System.Windows.Forms.ComboBox();
            this.btnRefreshLog = new System.Windows.Forms.Button();
            this.lblLogPath = new System.Windows.Forms.Label();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.pnlButtons = new System.Windows.Forms.Panel();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnPreview = new System.Windows.Forms.Button();
            this.btnRunNow = new System.Windows.Forms.Button();
            this.btnDryRun = new System.Windows.Forms.Button();
            this.btnToggleEnabled = new System.Windows.Forms.Button();
            this.btnOpenLogs = new System.Windows.Forms.Button();
            this.btnConnectivity = new System.Windows.Forms.Button();
            this.btnCredentials = new System.Windows.Forms.Button();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.status = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.SuspendLayout();
            //
            // pnlElevation
            //
            this.pnlElevation.BackColor = System.Drawing.Color.FromArgb(255, 248, 220);
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
            this.pnlJobsHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlJobsHeader.Location = new System.Drawing.Point(10, 10);
            this.pnlJobsHeader.Name = "pnlJobsHeader";
            this.pnlJobsHeader.Size = new System.Drawing.Size(1032, 66);
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
            this.lblVoxRootHint.Location = new System.Drawing.Point(160, 37);
            this.lblVoxRootHint.Name = "lblVoxRootHint";
            this.lblVoxRootHint.Size = new System.Drawing.Size(700, 15);
            this.lblVoxRootHint.TabIndex = 3;
            this.lblVoxRootHint.Text = "Backup_Folder1.bat, Backup_Folder2.bat and Backup_Folder3.bat are written here, and logs go to its Logs subfolder.";
            //
            // pnlJobEditors
            //
            this.pnlJobEditors.AutoScroll = true;
            this.pnlJobEditors.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlJobEditors.Location = new System.Drawing.Point(10, 76);
            this.pnlJobEditors.Name = "pnlJobEditors";
            this.pnlJobEditors.Size = new System.Drawing.Size(1032, 413);
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
            this.pnlButtons.Controls.Add(this.btnPreview);
            this.pnlButtons.Controls.Add(this.btnRunNow);
            this.pnlButtons.Controls.Add(this.btnDryRun);
            this.pnlButtons.Controls.Add(this.btnToggleEnabled);
            this.pnlButtons.Controls.Add(this.btnOpenLogs);
            this.pnlButtons.Controls.Add(this.btnConnectivity);
            this.pnlButtons.Controls.Add(this.btnCredentials);
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
            this.btnSave.Size = new System.Drawing.Size(150, 32);
            this.btnSave.TabIndex = 0;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            //
            // btnPreview
            //
            this.btnPreview.Location = new System.Drawing.Point(168, 10);
            this.btnPreview.Name = "btnPreview";
            this.btnPreview.Size = new System.Drawing.Size(110, 32);
            this.btnPreview.TabIndex = 1;
            this.btnPreview.Text = "Preview";
            this.btnPreview.UseVisualStyleBackColor = true;
            this.btnPreview.Click += new System.EventHandler(this.btnPreview_Click);
            //
            // btnRunNow
            //
            this.btnRunNow.Location = new System.Drawing.Point(284, 10);
            this.btnRunNow.Name = "btnRunNow";
            this.btnRunNow.Size = new System.Drawing.Size(100, 32);
            this.btnRunNow.TabIndex = 2;
            this.btnRunNow.Text = "Run now";
            this.btnRunNow.UseVisualStyleBackColor = true;
            this.btnRunNow.Click += new System.EventHandler(this.btnRunNow_Click);
            //
            // btnDryRun
            //
            this.btnDryRun.Location = new System.Drawing.Point(390, 10);
            this.btnDryRun.Name = "btnDryRun";
            this.btnDryRun.Size = new System.Drawing.Size(120, 32);
            this.btnDryRun.TabIndex = 3;
            this.btnDryRun.Text = "Test (dry run)";
            this.btnDryRun.UseVisualStyleBackColor = true;
            this.btnDryRun.Click += new System.EventHandler(this.btnDryRun_Click);
            //
            // btnToggleEnabled
            //
            this.btnToggleEnabled.Location = new System.Drawing.Point(516, 10);
            this.btnToggleEnabled.Name = "btnToggleEnabled";
            this.btnToggleEnabled.Size = new System.Drawing.Size(120, 32);
            this.btnToggleEnabled.TabIndex = 4;
            this.btnToggleEnabled.Text = "Enable/Disable";
            this.btnToggleEnabled.UseVisualStyleBackColor = true;
            this.btnToggleEnabled.Click += new System.EventHandler(this.btnToggleEnabled_Click);
            //
            // btnOpenLogs
            //
            this.btnOpenLogs.Location = new System.Drawing.Point(642, 10);
            this.btnOpenLogs.Name = "btnOpenLogs";
            this.btnOpenLogs.Size = new System.Drawing.Size(100, 32);
            this.btnOpenLogs.TabIndex = 5;
            this.btnOpenLogs.Text = "Open logs";
            this.btnOpenLogs.UseVisualStyleBackColor = true;
            this.btnOpenLogs.Click += new System.EventHandler(this.btnOpenLogs_Click);
            //
            // btnConnectivity
            //
            this.btnConnectivity.Location = new System.Drawing.Point(748, 10);
            this.btnConnectivity.Name = "btnConnectivity";
            this.btnConnectivity.Size = new System.Drawing.Size(130, 32);
            this.btnConnectivity.TabIndex = 6;
            this.btnConnectivity.Text = "Check connectivity";
            this.btnConnectivity.UseVisualStyleBackColor = true;
            this.btnConnectivity.Click += new System.EventHandler(this.btnConnectivity_Click);
            //
            // btnCredentials
            //
            this.btnCredentials.Location = new System.Drawing.Point(884, 10);
            this.btnCredentials.Name = "btnCredentials";
            this.btnCredentials.Size = new System.Drawing.Size(130, 32);
            this.btnCredentials.TabIndex = 7;
            this.btnCredentials.Text = "Check credentials";
            this.btnCredentials.UseVisualStyleBackColor = true;
            this.btnCredentials.Click += new System.EventHandler(this.btnCredentials_Click);
            //
            // btnRefresh
            //
            this.btnRefresh.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top
                | System.Windows.Forms.AnchorStyles.Right));
            this.btnRefresh.Location = new System.Drawing.Point(1020, 10);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(28, 32);
            this.btnRefresh.TabIndex = 8;
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
