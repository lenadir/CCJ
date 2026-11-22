using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using CCJ.Models;

namespace CCJ.Forms
{
    /// <summary>Shows the result of the connectivity and credential checks, one line per check.</summary>
    public class TextReportForm : Form
    {
        private readonly TextBox _text;

        public TextReportForm(string title, string caption, IEnumerable<CheckResult> results,
                              string extraButtonText, EventHandler extraButtonClick)
        {
            this.Text = title;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.Size = new Size(760, 480);
            this.MinimumSize = new Size(520, 320);

            Label captionLabel = new Label();
            captionLabel.Dock = DockStyle.Top;
            captionLabel.Height = 36;
            captionLabel.Padding = new Padding(12, 12, 12, 0);
            captionLabel.Text = caption;

            StringBuilder builder = new StringBuilder();
            bool allOk = true;
            int count = 0;

            foreach (CheckResult result in results)
            {
                builder.AppendLine(result.ToString());
                if (!result.Ok) allOk = false;
                count++;
            }

            if (count == 0)
                builder.AppendLine("Nothing was checked.");

            _text = new TextBox();
            _text.Dock = DockStyle.Fill;
            _text.Multiline = true;
            _text.ReadOnly = true;
            _text.ScrollBars = ScrollBars.Both;
            _text.WordWrap = false;
            _text.Font = new Font("Consolas", 9f);
            _text.BackColor = Color.White;
            _text.Text = builder.ToString();

            Label summary = new Label();
            summary.Dock = DockStyle.Bottom;
            summary.Height = 28;
            summary.Padding = new Padding(12, 6, 12, 0);
            summary.ForeColor = allOk ? Color.DarkGreen : Color.Firebrick;
            summary.Text = count == 0
                ? ""
                : (allOk ? "All " + count + " checks passed." : "Some checks failed - see above.");

            Panel buttons = new Panel();
            buttons.Dock = DockStyle.Bottom;
            buttons.Height = 52;

            Button close = new Button();
            close.Text = "Close";
            close.DialogResult = DialogResult.OK;
            close.Size = new Size(110, 30);
            close.Location = new Point(this.ClientSize.Width - 124, 11);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttons.Controls.Add(close);

            if (!string.IsNullOrEmpty(extraButtonText) && extraButtonClick != null)
            {
                Button extra = new Button();
                extra.Text = extraButtonText;
                extra.Size = new Size(230, 30);
                extra.Location = new Point(12, 11);
                extra.Click += extraButtonClick;
                buttons.Controls.Add(extra);
            }

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.Padding = new Padding(12, 0, 12, 0);
            body.Controls.Add(_text);

            this.Controls.Add(body);
            this.Controls.Add(captionLabel);
            this.Controls.Add(summary);
            this.Controls.Add(buttons);

            this.AcceptButton = close;
            this.CancelButton = close;
        }
    }
}
