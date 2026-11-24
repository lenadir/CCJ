namespace CCJ.Forms
{
    partial class JobEditorControl
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.GroupBox grpJob;
        private System.Windows.Forms.Label lblJobName;
        private System.Windows.Forms.TextBox txtJobName;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Label lblSource;
        private System.Windows.Forms.TextBox txtSource;
        private System.Windows.Forms.Label lblNasRoot;
        private System.Windows.Forms.TextBox txtNasRoot;
        private System.Windows.Forms.Label lblNasFolder;
        private System.Windows.Forms.TextBox txtNasFolder;
        private System.Windows.Forms.Label lblFirstRun;
        private System.Windows.Forms.DateTimePicker dtpFirstRun;
        private System.Windows.Forms.Label lblTime;
        private System.Windows.Forms.DateTimePicker dtpTime;
        private System.Windows.Forms.CheckBox chkEnabled;
        private System.Windows.Forms.Label lblDestinationCaption;
        private System.Windows.Forms.Label lblDestination;
        private System.Windows.Forms.Label lblProblems;

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
            this.grpJob = new System.Windows.Forms.GroupBox();
            this.lblJobName = new System.Windows.Forms.Label();
            this.txtJobName = new System.Windows.Forms.TextBox();
            this.btnRemove = new System.Windows.Forms.Button();
            this.lblSource = new System.Windows.Forms.Label();
            this.txtSource = new System.Windows.Forms.TextBox();
            this.lblNasRoot = new System.Windows.Forms.Label();
            this.txtNasRoot = new System.Windows.Forms.TextBox();
            this.lblNasFolder = new System.Windows.Forms.Label();
            this.txtNasFolder = new System.Windows.Forms.TextBox();
            this.lblFirstRun = new System.Windows.Forms.Label();
            this.dtpFirstRun = new System.Windows.Forms.DateTimePicker();
            this.lblTime = new System.Windows.Forms.Label();
            this.dtpTime = new System.Windows.Forms.DateTimePicker();
            this.chkEnabled = new System.Windows.Forms.CheckBox();
            this.lblDestinationCaption = new System.Windows.Forms.Label();
            this.lblDestination = new System.Windows.Forms.Label();
            this.lblProblems = new System.Windows.Forms.Label();
            this.grpJob.SuspendLayout();
            this.SuspendLayout();
            //
            // grpJob
            //
            this.grpJob.Controls.Add(this.lblJobName);
            this.grpJob.Controls.Add(this.txtJobName);
            this.grpJob.Controls.Add(this.btnRemove);
            this.grpJob.Controls.Add(this.lblSource);
            this.grpJob.Controls.Add(this.txtSource);
            this.grpJob.Controls.Add(this.lblNasRoot);
            this.grpJob.Controls.Add(this.txtNasRoot);
            this.grpJob.Controls.Add(this.lblNasFolder);
            this.grpJob.Controls.Add(this.txtNasFolder);
            this.grpJob.Controls.Add(this.lblFirstRun);
            this.grpJob.Controls.Add(this.dtpFirstRun);
            this.grpJob.Controls.Add(this.lblTime);
            this.grpJob.Controls.Add(this.dtpTime);
            this.grpJob.Controls.Add(this.chkEnabled);
            this.grpJob.Controls.Add(this.lblDestinationCaption);
            this.grpJob.Controls.Add(this.lblDestination);
            this.grpJob.Controls.Add(this.lblProblems);
            this.grpJob.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpJob.Location = new System.Drawing.Point(0, 0);
            this.grpJob.Name = "grpJob";
            this.grpJob.Padding = new System.Windows.Forms.Padding(10);
            this.grpJob.Size = new System.Drawing.Size(900, 212);
            this.grpJob.TabIndex = 0;
            this.grpJob.TabStop = false;
            this.grpJob.Text = "Folder1";
            //
            // lblJobName
            //
            this.lblJobName.AutoSize = true;
            this.lblJobName.Location = new System.Drawing.Point(16, 28);
            this.lblJobName.Name = "lblJobName";
            this.lblJobName.Size = new System.Drawing.Size(60, 15);
            this.lblJobName.TabIndex = 0;
            this.lblJobName.Text = "Job name";
            //
            // txtJobName
            //
            this.txtJobName.Location = new System.Drawing.Point(160, 25);
            this.txtJobName.Name = "txtJobName";
            this.txtJobName.Size = new System.Drawing.Size(240, 23);
            this.txtJobName.TabIndex = 1;
            //
            // btnRemove
            //
            this.btnRemove.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top
                | System.Windows.Forms.AnchorStyles.Right));
            this.btnRemove.Location = new System.Drawing.Point(766, 24);
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(110, 25);
            this.btnRemove.TabIndex = 2;
            this.btnRemove.Text = "Remove job";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            //
            // lblSource
            //
            this.lblSource.AutoSize = true;
            this.lblSource.Location = new System.Drawing.Point(16, 59);
            this.lblSource.Name = "lblSource";
            this.lblSource.Size = new System.Drawing.Size(70, 15);
            this.lblSource.TabIndex = 3;
            this.lblSource.Text = "Source (UNC)";
            //
            // txtSource
            //
            this.txtSource.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top
                | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right));
            this.txtSource.Location = new System.Drawing.Point(160, 56);
            this.txtSource.Name = "txtSource";
            this.txtSource.Size = new System.Drawing.Size(716, 23);
            this.txtSource.TabIndex = 4;
            //
            // lblNasRoot
            //
            this.lblNasRoot.AutoSize = true;
            this.lblNasRoot.Location = new System.Drawing.Point(16, 90);
            this.lblNasRoot.Name = "lblNasRoot";
            this.lblNasRoot.Size = new System.Drawing.Size(84, 15);
            this.lblNasRoot.TabIndex = 5;
            this.lblNasRoot.Text = "NAS root (UNC)";
            //
            // txtNasRoot
            //
            this.txtNasRoot.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top
                | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right));
            this.txtNasRoot.Location = new System.Drawing.Point(160, 87);
            this.txtNasRoot.Name = "txtNasRoot";
            this.txtNasRoot.Size = new System.Drawing.Size(716, 23);
            this.txtNasRoot.TabIndex = 6;
            //
            // lblNasFolder
            //
            this.lblNasFolder.AutoSize = true;
            this.lblNasFolder.Location = new System.Drawing.Point(16, 121);
            this.lblNasFolder.Name = "lblNasFolder";
            this.lblNasFolder.Size = new System.Drawing.Size(99, 15);
            this.lblNasFolder.TabIndex = 7;
            this.lblNasFolder.Text = "NAS folder name";
            //
            // txtNasFolder
            //
            this.txtNasFolder.Location = new System.Drawing.Point(160, 118);
            this.txtNasFolder.Name = "txtNasFolder";
            this.txtNasFolder.Size = new System.Drawing.Size(240, 23);
            this.txtNasFolder.TabIndex = 8;
            //
            // lblFirstRun
            //
            this.lblFirstRun.AutoSize = true;
            this.lblFirstRun.Location = new System.Drawing.Point(16, 152);
            this.lblFirstRun.Name = "lblFirstRun";
            this.lblFirstRun.Size = new System.Drawing.Size(120, 15);
            this.lblFirstRun.TabIndex = 9;
            this.lblFirstRun.Text = "First run";
            //
            // dtpFirstRun
            //
            this.dtpFirstRun.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dtpFirstRun.CustomFormat = "yyyy-MM-dd  dddd";
            this.dtpFirstRun.Location = new System.Drawing.Point(160, 149);
            this.dtpFirstRun.Name = "dtpFirstRun";
            this.dtpFirstRun.Size = new System.Drawing.Size(190, 23);
            this.dtpFirstRun.TabIndex = 10;
            //
            // lblTime
            //
            this.lblTime.AutoSize = true;
            this.lblTime.Location = new System.Drawing.Point(362, 152);
            this.lblTime.Name = "lblTime";
            this.lblTime.Size = new System.Drawing.Size(36, 15);
            this.lblTime.TabIndex = 11;
            this.lblTime.Text = "at";
            //
            // dtpTime
            //
            this.dtpTime.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dtpTime.CustomFormat = "HH:mm";
            this.dtpTime.ShowUpDown = true;
            this.dtpTime.Location = new System.Drawing.Point(392, 149);
            this.dtpTime.Name = "dtpTime";
            this.dtpTime.Size = new System.Drawing.Size(80, 23);
            this.dtpTime.TabIndex = 12;
            //
            // chkEnabled
            //
            this.chkEnabled.AutoSize = true;
            this.chkEnabled.Location = new System.Drawing.Point(496, 152);
            this.chkEnabled.Name = "chkEnabled";
            this.chkEnabled.Size = new System.Drawing.Size(68, 19);
            this.chkEnabled.TabIndex = 13;
            this.chkEnabled.Text = "Enabled";
            this.chkEnabled.UseVisualStyleBackColor = true;
            //
            // lblDestinationCaption
            //
            this.lblDestinationCaption.AutoSize = true;
            this.lblDestinationCaption.Location = new System.Drawing.Point(16, 182);
            this.lblDestinationCaption.Name = "lblDestinationCaption";
            this.lblDestinationCaption.Size = new System.Drawing.Size(67, 15);
            this.lblDestinationCaption.TabIndex = 14;
            this.lblDestinationCaption.Text = "Destination";
            //
            // lblDestination
            //
            this.lblDestination.AutoEllipsis = true;
            this.lblDestination.Location = new System.Drawing.Point(160, 182);
            this.lblDestination.Name = "lblDestination";
            this.lblDestination.Size = new System.Drawing.Size(460, 15);
            this.lblDestination.TabIndex = 15;
            this.lblDestination.Text = "-";
            //
            // lblProblems
            //
            this.lblProblems.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top
                | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right));
            this.lblProblems.AutoEllipsis = true;
            this.lblProblems.ForeColor = System.Drawing.Color.Firebrick;
            this.lblProblems.Location = new System.Drawing.Point(628, 182);
            this.lblProblems.Name = "lblProblems";
            this.lblProblems.Size = new System.Drawing.Size(248, 15);
            this.lblProblems.TabIndex = 16;
            this.lblProblems.Tag = "error";
            this.lblProblems.Text = "";
            //
            // JobEditorControl
            //
            this.Controls.Add(this.grpJob);
            this.Name = "JobEditorControl";
            this.Size = new System.Drawing.Size(900, 212);
            this.grpJob.ResumeLayout(false);
            this.grpJob.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}
