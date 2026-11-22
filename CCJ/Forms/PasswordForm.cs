using System;
using System.Drawing;
using System.Windows.Forms;

namespace CCJ.Forms
{
    /// <summary>
    /// Asks for the Windows password needed to register a task that runs while the user is logged
    /// off. The value is handed to the caller once and the text box is cleared on close. CCJ never
    /// writes it to jobs.json, app.log or any other file.
    /// </summary>
    public class PasswordForm : Form
    {
        private readonly TextBox _user;
        private readonly TextBox _password;

        public PasswordForm(string defaultUserName, string reason)
        {
            this.Text = "Windows password";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ShowIcon = false;
            this.ClientSize = new Size(560, 236);

            Label explain = new Label();
            explain.Location = new Point(16, 14);
            explain.Size = new Size(528, 60);
            explain.Text = reason + "\r\n\r\nThe password is used once, to register the task with " +
                           "Windows. It is not stored by CCJ.";

            Label userCaption = new Label();
            userCaption.Location = new Point(16, 92);
            userCaption.Size = new Size(110, 18);
            userCaption.Text = "Run task as";

            _user = new TextBox();
            _user.Location = new Point(132, 89);
            _user.Size = new Size(412, 23);
            _user.Text = defaultUserName;

            Label passwordCaption = new Label();
            passwordCaption.Location = new Point(16, 126);
            passwordCaption.Size = new Size(110, 18);
            passwordCaption.Text = "Password";

            _password = new TextBox();
            _password.Location = new Point(132, 123);
            _password.Size = new Size(412, 23);
            _password.UseSystemPasswordChar = true;

            Label hint = new Label();
            hint.Location = new Point(132, 152);
            hint.Size = new Size(412, 34);
            hint.ForeColor = Color.DimGray;
            hint.Text = "An account with a blank password cannot run a task while logged off.";

            Button ok = new Button();
            ok.Text = "Save task";
            ok.DialogResult = DialogResult.OK;
            ok.Size = new Size(120, 30);
            ok.Location = new Point(this.ClientSize.Width - 254, this.ClientSize.Height - 42);

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Size = new Size(110, 30);
            cancel.Location = new Point(this.ClientSize.Width - 124, this.ClientSize.Height - 42);

            this.Controls.Add(explain);
            this.Controls.Add(userCaption);
            this.Controls.Add(_user);
            this.Controls.Add(passwordCaption);
            this.Controls.Add(_password);
            this.Controls.Add(hint);
            this.Controls.Add(ok);
            this.Controls.Add(cancel);

            this.AcceptButton = ok;
            this.CancelButton = cancel;
        }

        public string UserName
        {
            get { return _user.Text.Trim(); }
        }

        /// <summary>Read this once, immediately after the dialog returns OK.</summary>
        public string Password
        {
            get { return _password.Text; }
        }

        /// <summary>Overwrites the password box so the value does not linger in the control.</summary>
        public void ClearPassword()
        {
            _password.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _password != null && !_password.IsDisposed)
                _password.Clear();

            base.Dispose(disposing);
        }
    }
}
