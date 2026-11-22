using System;
using System.Drawing;
using System.Windows.Forms;

namespace CCJ.Forms
{
    /// <summary>
    /// Shows the generated script before anything is written to disk. The caller only writes when
    /// this returns DialogResult.OK.
    /// </summary>
    public class PreviewForm : Form
    {
        private readonly TextBox _text;
        private readonly Button _confirm;
        private readonly Button _cancel;
        private readonly Label _caption;

        public PreviewForm(string title, string caption, string content, string confirmText)
        {
            this.Text = title;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.Size = new Size(860, 680);
            this.MinimumSize = new Size(560, 400);

            _caption = new Label();
            _caption.Dock = DockStyle.Top;
            _caption.Height = 44;
            _caption.Padding = new Padding(12, 12, 12, 0);
            _caption.Text = caption;

            _text = new TextBox();
            _text.Dock = DockStyle.Fill;
            _text.Multiline = true;
            _text.ReadOnly = true;
            _text.ScrollBars = ScrollBars.Both;
            _text.WordWrap = false;
            _text.Font = new Font("Consolas", 9f);
            _text.Text = content;
            _text.BackColor = Color.White;

            Panel buttons = new Panel();
            buttons.Dock = DockStyle.Bottom;
            buttons.Height = 52;

            _confirm = new Button();
            _confirm.Text = string.IsNullOrEmpty(confirmText) ? "Write" : confirmText;
            _confirm.DialogResult = DialogResult.OK;
            _confirm.Size = new Size(190, 30);
            _confirm.Location = new Point(this.ClientSize.Width - 320, 11);
            _confirm.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            _cancel = new Button();
            _cancel.Text = "Cancel";
            _cancel.DialogResult = DialogResult.Cancel;
            _cancel.Size = new Size(110, 30);
            _cancel.Location = new Point(this.ClientSize.Width - 124, 11);
            _cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            buttons.Controls.Add(_confirm);
            buttons.Controls.Add(_cancel);

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.Padding = new Padding(12, 0, 12, 0);
            body.Controls.Add(_text);

            this.Controls.Add(body);
            this.Controls.Add(_caption);
            this.Controls.Add(buttons);

            this.AcceptButton = _confirm;
            this.CancelButton = _cancel;
        }
    }
}
