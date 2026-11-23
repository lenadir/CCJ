using System;
using System.Drawing;
using System.Windows.Forms;

namespace CCJ.Forms
{
    /// <summary>
    /// Light and dark colours for the whole window.
    ///
    /// WinForms has no dark mode of its own, so the colours are applied control by control. Each
    /// control says what it is through its <see cref="Control.Tag"/> - "hint", "error", "surface",
    /// "banner" or "readonly" - and the rest follows from the control's type, so a new control
    /// only has to be added to a form, not to a list here.
    ///
    /// Two things stay light on purpose even in dark mode: the progress bars in the grid, and the
    /// drop down calendar of the date pickers. The bars keep a light track because their fills are
    /// light and the text on them is black, which is the only way one colour of text stays
    /// readable both on the track and on the fill.
    /// </summary>
    public static class Theme
    {
        public const string TagHint = "hint";
        public const string TagError = "error";
        public const string TagSurface = "surface";
        public const string TagBanner = "banner";
        public const string TagReadOnly = "readonly";

        private static bool _dark;

        /// <summary>True while dark mode is on.</summary>
        public static bool Dark { get { return _dark; } }

        public static void Use(bool dark)
        {
            _dark = dark;
        }

        // ---------------------------------------------------------------- the palette

        public static Color Back
        {
            get { return _dark ? Color.FromArgb(32, 33, 36) : SystemColors.Control; }
        }

        /// <summary>A panel that should stand slightly apart from the window behind it.</summary>
        public static Color Surface
        {
            get { return _dark ? Color.FromArgb(44, 46, 51) : Color.FromArgb(244, 247, 251); }
        }

        /// <summary>Text boxes, combo boxes and the grid's rows.</summary>
        public static Color Field
        {
            get { return _dark ? Color.FromArgb(24, 25, 28) : Color.White; }
        }

        public static Color Text
        {
            get { return _dark ? Color.FromArgb(232, 234, 237) : SystemColors.ControlText; }
        }

        public static Color SubtleText
        {
            get { return _dark ? Color.FromArgb(154, 160, 166) : Color.DimGray; }
        }

        public static Color ErrorText
        {
            get { return _dark ? Color.FromArgb(255, 138, 128) : Color.Firebrick; }
        }

        public static Color WarningText
        {
            get { return _dark ? Color.FromArgb(252, 196, 110) : Color.DarkOrange; }
        }

        public static Color Border
        {
            get { return _dark ? Color.FromArgb(64, 67, 72) : SystemColors.ControlDark; }
        }

        public static Color ButtonBack
        {
            get { return _dark ? Color.FromArgb(54, 57, 63) : SystemColors.Control; }
        }

        public static Color Banner
        {
            get { return _dark ? Color.FromArgb(63, 51, 24) : Color.FromArgb(255, 248, 220); }
        }

        public static Color BannerText
        {
            get { return _dark ? Color.FromArgb(255, 226, 168) : SystemColors.ControlText; }
        }

        public static Color Selection
        {
            get { return _dark ? Color.FromArgb(58, 74, 99) : SystemColors.Highlight; }
        }

        public static Color SelectionText
        {
            get { return _dark ? Color.FromArgb(240, 240, 240) : SystemColors.HighlightText; }
        }

        // ---------------------------------------------------------------- applying it

        /// <summary>Colours a form and everything inside it. Safe to call again after a switch.</summary>
        public static void Apply(Control control)
        {
            if (control == null) return;

            ApplyOne(control);

            foreach (Control child in control.Controls)
                Apply(child);
        }

        private static void ApplyOne(Control control)
        {
            string tag = control.Tag as string;

            Form form = control as Form;
            if (form != null)
            {
                form.BackColor = Back;
                form.ForeColor = Text;
                return;
            }

            DataGridView grid = control as DataGridView;
            if (grid != null)
            {
                ApplyGrid(grid);
                return;
            }

            StatusStrip strip = control as StatusStrip;
            if (strip != null)
            {
                strip.BackColor = Surface;
                strip.ForeColor = Text;
                foreach (ToolStripItem item in strip.Items)
                    item.ForeColor = Text;
                return;
            }

            TabControl tabs = control as TabControl;
            if (tabs != null)
            {
                // Flat buttons let the strip beside the tabs take our colour; with the normal
                // appearance Windows paints that strip itself and it stays white.
                tabs.Appearance = _dark ? TabAppearance.FlatButtons : TabAppearance.Normal;
                tabs.BackColor = Back;
                tabs.ForeColor = Text;
                return;
            }

            TabPage page = control as TabPage;
            if (page != null)
            {
                page.UseVisualStyleBackColor = false;
                page.BackColor = Back;
                page.ForeColor = Text;
                return;
            }

            Button button = control as Button;
            if (button != null)
            {
                if (_dark)
                {
                    button.UseVisualStyleBackColor = false;
                    button.FlatStyle = FlatStyle.Flat;
                    button.BackColor = ButtonBack;
                    button.ForeColor = Text;
                    button.FlatAppearance.BorderColor = Border;
                    button.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 74, 81);
                    button.FlatAppearance.MouseDownBackColor = Color.FromArgb(84, 88, 96);
                }
                else
                {
                    button.FlatStyle = FlatStyle.Standard;
                    button.BackColor = SystemColors.Control;
                    button.ForeColor = SystemColors.ControlText;
                    button.UseVisualStyleBackColor = true;
                }
                return;
            }

            TextBox box = control as TextBox;
            if (box != null)
            {
                bool readOnlyRow = string.Equals(tag, TagReadOnly, StringComparison.Ordinal);
                box.BackColor = readOnlyRow ? BackOf(box.Parent) : Field;
                box.ForeColor = Text;
                return;
            }

            ComboBox combo = control as ComboBox;
            if (combo != null)
            {
                combo.FlatStyle = _dark ? FlatStyle.Flat : FlatStyle.Standard;
                combo.BackColor = Field;
                combo.ForeColor = Text;
                return;
            }

            DateTimePicker picker = control as DateTimePicker;
            if (picker != null)
            {
                // The box itself is drawn by Windows and ignores these; the calendar does not.
                picker.CalendarMonthBackground = Field;
                picker.CalendarForeColor = Text;
                picker.CalendarTitleBackColor = Surface;
                picker.CalendarTitleForeColor = Text;
                picker.CalendarTrailingForeColor = SubtleText;
                return;
            }

            CheckBox check = control as CheckBox;
            if (check != null)
            {
                // Left to Windows on purpose: a flat box in dark mode draws a pale tick on a pale
                // square, and whether a job is enabled has to be readable at a glance.
                check.FlatStyle = FlatStyle.Standard;
                check.BackColor = Color.Transparent;
                check.ForeColor = ForeFor(tag, check.Parent);
                return;
            }

            Label label = control as Label;
            if (label != null)
            {
                label.BackColor = string.Equals(tag, TagSurface, StringComparison.Ordinal)
                    ? Surface
                    : Color.Transparent;
                label.ForeColor = ForeFor(tag, label.Parent);
                return;
            }

            // Panels, group boxes and user controls.
            control.BackColor = string.Equals(tag, TagSurface, StringComparison.Ordinal)
                ? Surface
                : (string.Equals(tag, TagBanner, StringComparison.Ordinal) ? Banner : Back);
            control.ForeColor = string.Equals(tag, TagBanner, StringComparison.Ordinal)
                ? BannerText
                : Text;
        }

        private static void ApplyGrid(DataGridView grid)
        {
            grid.EnableHeadersVisualStyles = !_dark;
            grid.BackgroundColor = Field;
            grid.GridColor = Border;
            grid.BorderStyle = BorderStyle.FixedSingle;

            grid.DefaultCellStyle.BackColor = Field;
            grid.DefaultCellStyle.ForeColor = Text;
            grid.DefaultCellStyle.SelectionBackColor = Selection;
            grid.DefaultCellStyle.SelectionForeColor = SelectionText;

            grid.ColumnHeadersDefaultCellStyle.BackColor = _dark ? Surface : SystemColors.Control;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = _dark ? Surface : SystemColors.Control;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Text;

            grid.RowHeadersDefaultCellStyle.BackColor = _dark ? Surface : SystemColors.Control;
            grid.RowHeadersDefaultCellStyle.ForeColor = Text;
        }

        /// <summary>Draws the tab headers, which Windows will not colour for us.</summary>
        public static void DrawTab(TabControl tabs, DrawItemEventArgs e)
        {
            if (tabs == null || e.Index < 0 || e.Index >= tabs.TabPages.Count) return;

            TabPage page = tabs.TabPages[e.Index];
            bool selected = tabs.SelectedIndex == e.Index;

            Color back = selected ? (_dark ? Back : Color.White) : (_dark ? Surface : SystemColors.Control);
            Color fore = selected ? Text : SubtleText;

            Rectangle bounds = e.Bounds;
            using (SolidBrush brush = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(brush, bounds);
            }

            using (Pen pen = new Pen(Border))
            {
                e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height);
            }

            if (selected)
            {
                using (SolidBrush accent = new SolidBrush(_dark ? Color.FromArgb(138, 180, 248) : Color.FromArgb(0, 102, 204)))
                {
                    e.Graphics.FillRectangle(accent, bounds.X + 1, bounds.Y, bounds.Width - 2, 3);
                }
            }

            TextRenderer.DrawText(e.Graphics, page.Text, tabs.Font, bounds, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        private static Color ForeFor(string tag, Control parent)
        {
            if (string.Equals(tag, TagHint, StringComparison.Ordinal)) return SubtleText;
            if (string.Equals(tag, TagError, StringComparison.Ordinal)) return ErrorText;

            if (parent != null && string.Equals(parent.Tag as string, TagBanner, StringComparison.Ordinal))
                return BannerText;

            return Text;
        }

        private static Color BackOf(Control parent)
        {
            if (parent == null) return Back;
            if (string.Equals(parent.Tag as string, TagSurface, StringComparison.Ordinal)) return Surface;
            if (string.Equals(parent.Tag as string, TagBanner, StringComparison.Ordinal)) return Banner;
            return Back;
        }
    }
}
