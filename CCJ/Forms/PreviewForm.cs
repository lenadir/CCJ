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

            // The buttons are laid out by the panel itself, right to left, rather than placed at
            // a position worked out from the form's width. Anchoring them to a panel that has not
            // been docked yet pushes them off the right edge when it is later stretched to the
            // width of the form, which leaves the dialog with no reachable buttons at all.
            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Bottom;
            buttons.Height = 52;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Padding = new Padding(0, 11, 12, 0);

            _confirm = new Button();
            _confirm.Text = string.IsNullOrEmpty(confirmText) ? "Write" : confirmText;
            _confirm.DialogResult = DialogResult.OK;
            _confirm.Size = new Size(190, 30);
            _confirm.Margin = new Padding(0, 0, 6, 0);

            _cancel = new Button();
            _cancel.Text = "Cancel";
            _cancel.DialogResult = DialogResult.Cancel;
            _cancel.Size = new Size(110, 30);
            _cancel.Margin = new Padding(0);

            // Right to left: the first one added sits furthest right.
            buttons.Controls.Add(_cancel);
            buttons.Controls.Add(_confirm);

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
