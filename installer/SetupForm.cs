using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CCJ.Setup
{
    /// <summary>
    /// One window, one button. The folders are pre-filled, so the whole install is: double-click,
    /// approve the Windows prompt, press Install.
    /// </summary>
    internal class SetupForm : Form
    {
        private readonly TextBox _installDir;
        private readonly TextBox _voxRoot;
        private readonly CheckBox _desktop;
        private readonly CheckBox _startMenu;
        private readonly Button _install;
        private readonly Button _close;
        private readonly Button _launch;
        private readonly TextBox _log;
        private readonly Label _heading;
        private readonly Label _subheading;

        private bool _installed;

        public SetupForm()
        {
            this.Text = "Install CCJ";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.ClientSize = new Size(620, 430);

            _heading = new Label();
            _heading.Location = new Point(20, 18);
            _heading.Size = new Size(580, 26);
            _heading.Font = new Font(this.Font.FontFamily, 12f, FontStyle.Bold);
            _heading.Text = "CCJ - Robocopy backup jobs";

            _subheading = new Label();
            _subheading.Location = new Point(20, 46);
            _subheading.Size = new Size(580, 38);
            _subheading.ForeColor = Color.DimGray;
            _subheading.Text = "This copies CCJ onto this PC, creates the script and log folder, and adds " +
                               "shortcuts. It does not change any backup job or touch the NAS.";

            Label installCaption = new Label();
            installCaption.Location = new Point(20, 96);
            installCaption.Size = new Size(150, 18);
            installCaption.Text = "Install CCJ in";

            _installDir = new TextBox();
            _installDir.Location = new Point(20, 116);
            _installDir.Size = new Size(480, 23);
            _installDir.Text = Program.DefaultInstallDir;

            Button browseInstall = new Button();
            browseInstall.Location = new Point(508, 115);
            browseInstall.Size = new Size(92, 25);
            browseInstall.Text = "Browse...";
            browseInstall.Click += new EventHandler(BrowseInstall);

            Label voxCaption = new Label();
            voxCaption.Location = new Point(20, 152);
            voxCaption.Size = new Size(300, 18);
            voxCaption.Text = "Script and log folder";

            _voxRoot = new TextBox();
            _voxRoot.Location = new Point(20, 172);
            _voxRoot.Size = new Size(480, 23);
            _voxRoot.Text = Program.DefaultVoxRoot;

            Button browseVox = new Button();
            browseVox.Location = new Point(508, 171);
            browseVox.Size = new Size(92, 25);
            browseVox.Text = "Browse...";
            browseVox.Click += new EventHandler(BrowseVox);

            Label voxHint = new Label();
            voxHint.Location = new Point(20, 198);
            voxHint.Size = new Size(580, 18);
            voxHint.ForeColor = Color.DimGray;
            voxHint.Text = "Backup_Folder1.bat, Backup_Folder2.bat, Backup_Folder3.bat and Logs\\master.log go here.";

            _startMenu = new CheckBox();
            _startMenu.Location = new Point(20, 226);
            _startMenu.Size = new Size(200, 20);
            _startMenu.Text = "Start menu shortcut";
            _startMenu.Checked = true;

            _desktop = new CheckBox();
            _desktop.Location = new Point(230, 226);
            _desktop.Size = new Size(200, 20);
            _desktop.Text = "Desktop shortcut";
            _desktop.Checked = true;

            _log = new TextBox();
            _log.Location = new Point(20, 256);
            _log.Size = new Size(580, 120);
            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;
            _log.BackColor = Color.White;
            _log.Font = new Font("Consolas", 8.5f);
            _log.Visible = false;

            _install = new Button();
            _install.Location = new Point(386, 388);
            _install.Size = new Size(114, 30);
            _install.Text = "Install";
            _install.Click += new EventHandler(DoInstall);

            _launch = new Button();
            _launch.Location = new Point(386, 388);
            _launch.Size = new Size(114, 30);
            _launch.Text = "Start CCJ";
            _launch.Visible = false;
            _launch.Click += new EventHandler(LaunchApp);

            _close = new Button();
            _close.Location = new Point(508, 388);
            _close.Size = new Size(92, 30);
            _close.Text = "Cancel";
            _close.Click += new EventHandler(CloseSetup);

            this.Controls.Add(_heading);
            this.Controls.Add(_subheading);
            this.Controls.Add(installCaption);
            this.Controls.Add(_installDir);
            this.Controls.Add(browseInstall);
            this.Controls.Add(voxCaption);
            this.Controls.Add(_voxRoot);
            this.Controls.Add(browseVox);
            this.Controls.Add(voxHint);
            this.Controls.Add(_startMenu);
            this.Controls.Add(_desktop);
            this.Controls.Add(_log);
            this.Controls.Add(_install);
            this.Controls.Add(_launch);
            this.Controls.Add(_close);

            this.AcceptButton = _install;
        }

        private void BrowseInstall(object sender, EventArgs e)
        {
            Pick(_installDir, "Folder to install CCJ into");
        }

        private void BrowseVox(object sender, EventArgs e)
        {
            Pick(_voxRoot, "Folder for the generated scripts and the Logs folder");
        }

        private void Pick(TextBox box, string description)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = description;
                if (Directory.Exists(box.Text)) dialog.SelectedPath = box.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) box.Text = dialog.SelectedPath;
            }
        }

        private void DoInstall(object sender, EventArgs e)
        {
            _log.Visible = true;
            _log.Clear();
            _install.Enabled = false;
            _close.Enabled = false;
            this.Cursor = Cursors.WaitCursor;

            try
            {
                Engine.Install(_installDir.Text.Trim(), _voxRoot.Text.Trim(),
                    _desktop.Checked, _startMenu.Checked, new Action<string>(Log));

                _installed = true;
                _heading.Text = "CCJ is installed";
                _subheading.Text = "Open it, then set up the three jobs on the Jobs tab and press Save. " +
                                   "The user manual is in the install folder.";
                _install.Visible = false;
                _launch.Visible = true;
                _close.Text = "Close";
            }
            catch (Exception ex)
            {
                Log("");
                Log("FAILED: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Setup could not finish",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _install.Enabled = true;
            }
            finally
            {
                _close.Enabled = true;
                this.Cursor = Cursors.Default;
            }
        }

        private void LaunchApp(object sender, EventArgs e)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(
                    Path.Combine(_installDir.Text.Trim(), "CCJ.exe"));
                info.UseShellExecute = true;
                info.WorkingDirectory = _installDir.Text.Trim();
                Process.Start(info);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start CCJ",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            this.Close();
        }

        private void CloseSetup(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Log(string line)
        {
            _log.AppendText(line + Environment.NewLine);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
            Application.DoEvents();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_installed && !_install.Enabled)
            {
                // An install is in progress; let it finish.
                e.Cancel = true;
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
