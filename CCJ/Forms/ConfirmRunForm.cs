using System;
using System.Drawing;
using System.Windows.Forms;
using CCJ.Models;
using CCJ.Services;

namespace CCJ.Forms
{
    /// <summary>
    /// The confirmation the spec requires before any real copy starts. It always shows the source
    /// path, the destination path and the file count, and the confirm button stays disabled until
    /// the count has been taken (or has definitively failed).
    /// </summary>
    public class ConfirmRunForm : Form
    {
        private readonly Button _confirm;
        private readonly Label _count;
        private readonly Label _plan;

        public ConfirmRunForm(JobConfig job, bool dryRun)
        {
            this.Text = dryRun ? "Confirm test (dry run)" : "Confirm backup run";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ShowIcon = false;
            this.ClientSize = new Size(680, dryRun ? 310 : 356);

            Label headline = new Label();
            headline.Location = new Point(16, 14);
            headline.Size = new Size(648, 44);
            headline.Font = new Font(this.Font, FontStyle.Bold);
            headline.Text = dryRun
                ? "Start a DRY RUN of \"" + job.JobName + "\"?\r\n" +
                  "robocopy runs with /L, so it only lists files. Nothing is copied or deleted."
                : "Start a REAL backup of \"" + job.JobName + "\" now?\r\n" +
                  "Files will be copied to the destination. Nothing is ever deleted.";

            int y = 70;
            AddRow("Source", job.SourcePath, ref y);
            AddRow("Destination", job.DestinationPath, ref y);

            Label countCaption = new Label();
            countCaption.Location = new Point(16, y + 3);
            countCaption.Size = new Size(110, 18);
            countCaption.Text = "Files in source";
            this.Controls.Add(countCaption);

            _count = new Label();
            _count.Location = new Point(132, y + 3);
            _count.Size = new Size(532, 18);
            _count.Text = "counting...";
            this.Controls.Add(_count);
            y += 34;

            if (!dryRun)
            {
                // What robocopy will really copy, found with a list-only pass. This is the number
                // the progress bar counts towards, and it shows up an unexpectedly large copy
                // before it starts.
                Label planCaption = new Label();
                planCaption.Location = new Point(16, y + 3);
                planCaption.Size = new Size(110, 18);
                planCaption.Text = "To copy now";
                this.Controls.Add(planCaption);

                _plan = new Label();
                _plan.Location = new Point(132, y + 3);
                _plan.Size = new Size(532, 36);
                _plan.Text = "working it out...";
                this.Controls.Add(_plan);
                y += 44;
            }

            Label note = new Label();
            note.Location = new Point(16, y + 6);
            note.Size = new Size(648, 60);
            note.ForeColor = Color.DimGray;
            note.Text = dryRun
                ? "The dry run script is written to your TEMP folder and logs there too. The NAS " +
                  "destination folder is not created."
                : "The task runs under the account stored in Task Scheduler, not necessarily the " +
                  "account you are using now. Options: " + Services.ScriptGenerator.RobocopyOptions +
                  " (no /MIR, no /PURGE).";

            this.Controls.Add(note);

            _confirm = new Button();
            _confirm.Text = dryRun ? "Run dry run" : "Yes, start the copy";
            _confirm.DialogResult = DialogResult.OK;
            _confirm.Size = new Size(170, 30);
            _confirm.Location = new Point(this.ClientSize.Width - 300, this.ClientSize.Height - 42);
            _confirm.Enabled = false;

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Size = new Size(110, 30);
            cancel.Location = new Point(this.ClientSize.Width - 124, this.ClientSize.Height - 42);

            this.Controls.Add(headline);
            this.Controls.Add(_confirm);
            this.Controls.Add(cancel);

            this.AcceptButton = _confirm;
            this.CancelButton = cancel;
        }

        private void AddRow(string caption, string value, ref int y)
        {
            Label captionLabel = new Label();
            captionLabel.Location = new Point(16, y);
            captionLabel.Size = new Size(110, 18);
            captionLabel.Text = caption;

            TextBox valueBox = new TextBox();
            valueBox.Location = new Point(132, y - 3);
            valueBox.Size = new Size(532, 23);
            valueBox.ReadOnly = true;
            valueBox.BorderStyle = BorderStyle.None;
            valueBox.BackColor = this.BackColor;
            valueBox.Text = string.IsNullOrEmpty(value) ? "(not set)" : value;

            this.Controls.Add(captionLabel);
            this.Controls.Add(valueBox);
            y += 30;
        }

        /// <summary>
        /// Called when the list-only pass has finished. If it could not run, that is said plainly:
        /// "unknown" is not the same as "nothing to copy".
        /// </summary>
        public void SetPlan(PlanResult plan)
        {
            if (_plan == null) return;

            if (plan == null || !plan.Complete)
            {
                string why = plan != null && !plan.Cancelled && !string.IsNullOrEmpty(plan.Error)
                    ? plan.Error
                    : "the check could not be completed.";
                _plan.ForeColor = Color.DimGray;
                _plan.Text = "unknown - " + why + " The copy can still go ahead, but the progress bar will not show a percentage.";
                return;
            }

            _plan.ForeColor = SystemColors.ControlText;
            if (plan.Files == 0)
            {
                _plan.Text = "nothing - the destination is already up to date.";
                return;
            }

            _plan.Text = plan.Files.ToString("N0") + " file(s), " + SizeFormat.Bytes(plan.Bytes) +
                         ". Found with a list-only check; nothing has been copied.";
        }

        /// <summary>
        /// Called once the count is known. A null count means it could not be read, which is shown
        /// as "unknown" rather than guessed, and the run can still be confirmed deliberately.
        /// </summary>
        public void SetFileCount(long? count)
        {
            if (count.HasValue)
                _count.Text = count.Value.ToString("N0") + " file(s) found under the source.";
            else
                _count.Text = "unknown - the source could not be listed (unreachable, or no access).";

            _confirm.Enabled = true;
        }
    }
}
