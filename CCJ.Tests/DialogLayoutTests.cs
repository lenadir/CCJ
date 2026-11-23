using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using CCJ.Forms;
using CCJ.Models;
using NUnit.Framework;

namespace CCJ.Tests
{
    /// <summary>
    /// Every dialog must have its buttons inside the window.
    ///
    /// This exists because of a real failure: PreviewForm placed its buttons at a position worked
    /// out from the form's width and anchored them to the right of a panel that had not been
    /// docked yet. When the panel was later stretched to the width of the form, the anchor pushed
    /// both buttons 644 pixels past the right edge. The dialog looked fine, but there was nothing
    /// to click, so Save and Test (dry run) could not be confirmed at all and quietly did nothing.
    ///
    /// A dialog whose confirm button cannot be reached is a dialog that cannot be used, so the
    /// check is on every button of every dialog, not only the two that were broken.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    [NonParallelizable]
    public class DialogLayoutTests
    {
        [Test]
        public void The_preview_dialog_keeps_its_buttons_inside_the_window()
        {
            using (PreviewForm form = new PreviewForm("Preview", "caption", "the script", "Write scripts and save"))
            {
                AssertButtonsAreUsable(form);
            }
        }

        [Test]
        public void The_preview_dialog_still_has_a_confirm_and_a_cancel()
        {
            using (PreviewForm form = new PreviewForm("Preview", "caption", "the script", "Write it"))
            {
                List<Button> buttons = Buttons(form);

                Assert.That(Texts(buttons), Does.Contain("Write it"));
                Assert.That(form.AcceptButton, Is.Not.Null, "Enter must confirm");
                Assert.That(form.CancelButton, Is.Not.Null, "Escape must cancel");
                Assert.That(((Button)form.AcceptButton).DialogResult, Is.EqualTo(DialogResult.OK));
                Assert.That(((Button)form.CancelButton).DialogResult, Is.EqualTo(DialogResult.Cancel));
            }
        }

        [Test]
        public void The_report_dialog_keeps_its_buttons_inside_the_window()
        {
            List<CheckResult> results = new List<CheckResult>();
            results.Add(new CheckResult("a check", true, "fine."));

            using (TextReportForm form = new TextReportForm("Report", "caption", results, null, null))
            {
                AssertButtonsAreUsable(form);
            }
        }

        [Test]
        public void The_report_dialog_keeps_its_extra_button_inside_the_window_too()
        {
            List<CheckResult> results = new List<CheckResult>();
            results.Add(new CheckResult("a check", false, "not fine."));

            using (TextReportForm form = new TextReportForm("Report", "caption", results,
                "Open Credential Manager", new EventHandler(delegate { })))
            {
                AssertButtonsAreUsable(form);
                Assert.That(Texts(Buttons(form)), Does.Contain("Open Credential Manager"));
            }
        }

        [Test]
        public void The_run_confirmation_keeps_its_buttons_inside_the_window()
        {
            JobConfig job = new JobConfig();
            job.Key = "Folder1";
            job.JobName = "Folder1";
            job.SourcePath = @"\\host\share";
            job.NasRoot = @"\\nas\share";
            job.NasFolder = "NADIR";
            job.FirstRunDate = JobsFile.NextSunday();

            using (ConfirmRunForm real = new ConfirmRunForm(job, false))
            {
                AssertButtonsAreUsable(real);
            }

            using (ConfirmRunForm dry = new ConfirmRunForm(job, true))
            {
                AssertButtonsAreUsable(dry);
            }
        }

        [Test]
        public void The_password_dialog_keeps_its_buttons_inside_the_window()
        {
            using (PasswordForm form = new PasswordForm("HP", "why the password is needed"))
            {
                AssertButtonsAreUsable(form);
            }
        }

        [Test]
        public void A_dialog_is_still_usable_after_the_theme_is_applied()
        {
            try
            {
                Theme.Use(true);
                using (PreviewForm form = new PreviewForm("Preview", "caption", "the script", "Write"))
                {
                    Theme.Apply(form);
                    AssertButtonsAreUsable(form);
                }
            }
            finally
            {
                Theme.Use(false);
            }
        }

        /// <summary>
        /// Every button must sit inside the client area and be visible and enabled, both at the
        /// size the dialog opens at and after it is resized, which is when an anchor goes wrong.
        /// </summary>
        private static void AssertButtonsAreUsable(Form form)
        {
            CheckAt(form, "as opened");

            if (form.FormBorderStyle == FormBorderStyle.Sizable ||
                form.FormBorderStyle == FormBorderStyle.SizableToolWindow)
            {
                form.Size = new Size(form.Size.Width + 260, form.Size.Height + 120);
                CheckAt(form, "after being made wider");

                form.Size = form.MinimumSize.IsEmpty
                    ? new Size(form.Size.Width - 260, form.Size.Height - 120)
                    : form.MinimumSize;
                CheckAt(form, "at its smallest");
            }
        }

        private static void CheckAt(Form form, string when)
        {
            // A dialog the user sees has a window and has been laid out. Both are forced here, so
            // the docked panels have the width they will really have, without showing anything.
            IntPtr unused = form.Handle;
            form.PerformLayout();

            Rectangle client = form.ClientRectangle;
            List<Button> buttons = Buttons(form);

            Assert.That(buttons, Is.Not.Empty, form.GetType().Name + " has no buttons at all");

            foreach (Button button in buttons)
            {
                Rectangle onForm = form.RectangleToClient(button.Parent.RectangleToScreen(button.Bounds));

                Assert.That(client.Contains(onForm), Is.True,
                    form.GetType().Name + " " + when + ": the button \"" + button.Text +
                    "\" is at " + onForm + ", outside the window's " + client +
                    ". Nobody can click it.");

                // Control.Visible is false for the children of a form that has never been shown,
                // so what can be checked here is that nothing was deliberately hidden.
                Assert.That(button.Parent, Is.Not.Null, "\"" + button.Text + "\" has no parent " + when);

                // The confirm button of the run dialog starts disabled on purpose, until the file
                // count is in. Every other button has to be usable straight away.
                Assert.That(button.Enabled || button.DialogResult == DialogResult.OK, Is.True,
                    "\"" + button.Text + "\" is disabled " + when);
            }
        }

        private static List<Button> Buttons(Control root)
        {
            List<Button> found = new List<Button>();
            foreach (Control child in root.Controls)
            {
                Button button = child as Button;
                if (button != null) found.Add(button);
                found.AddRange(Buttons(child));
            }
            return found;
        }

        private static List<string> Texts(List<Button> buttons)
        {
            List<string> texts = new List<string>();
            foreach (Button button in buttons) texts.Add(button.Text);
            return texts;
        }
    }
}
