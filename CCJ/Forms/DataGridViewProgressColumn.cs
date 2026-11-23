using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CCJ.Models;

namespace CCJ.Forms
{
    /// <summary>A grid column that shows a <see cref="CopyProgress"/> as a progress bar.</summary>
    public class DataGridViewProgressColumn : DataGridViewColumn
    {
        public DataGridViewProgressColumn()
            : base(new DataGridViewProgressCell())
        {
            this.ReadOnly = true;
            this.SortMode = DataGridViewColumnSortMode.NotSortable;
        }
    }

    /// <summary>
    /// Draws its own bar: a grey track, a coloured fill up to the percentage, and the text on top.
    ///
    /// The fills are light on purpose, so one colour of text (black) is readable on the track and
    /// on the fill alike, wherever the edge of the bar happens to fall under the text.
    ///
    /// A bar with no known total (the count is still being worked out) is filled with diagonal
    /// stripes instead of a length, because drawing a length would be a guess.
    /// </summary>
    public class DataGridViewProgressCell : DataGridViewTextBoxCell
    {
        private static readonly Color Track = Color.FromArgb(236, 236, 236);
        private static readonly Color TrackBorder = Color.FromArgb(160, 160, 160);

        public static Color FillColor(CopyPhase phase)
        {
            switch (phase)
            {
                case CopyPhase.Finished: return Color.FromArgb(150, 212, 164);
                case CopyPhase.Failed: return Color.FromArgb(238, 150, 150);
                case CopyPhase.Stopped: return Color.FromArgb(244, 196, 130);
                case CopyPhase.Paused: return Color.FromArgb(176, 186, 202);
                default: return Color.FromArgb(143, 185, 235);
            }
        }

        /// <summary>The grid would otherwise try to convert the value to a string itself.</summary>
        protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle,
            TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter,
            DataGridViewDataErrorContexts context)
        {
            return value == null ? "" : value.ToString();
        }

        protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
            DataGridViewElementStates cellState, object value, object formattedValue, string errorText,
            DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle,
            DataGridViewPaintParts paintParts)
        {
            // Background, selection colour and borders. The bar and its text are drawn below.
            base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, null, string.Empty, errorText,
                cellStyle, advancedBorderStyle, paintParts & ~DataGridViewPaintParts.ContentForeground);

            if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0) return;

            CopyProgress progress = value as CopyProgress;
            Rectangle slot = new Rectangle(cellBounds.X + 4, cellBounds.Y + 4, cellBounds.Width - 9, cellBounds.Height - 9);
            if (slot.Width < 12 || slot.Height < 6) return;

            Font font = cellStyle.Font ?? SystemFonts.DefaultFont;

            if (progress == null)
            {
                // The cell's own colour, so the dash is visible in dark mode too.
                TextRenderer.DrawText(graphics, "-", font, slot, cellStyle.ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                return;
            }

            Rectangle inner = new Rectangle(slot.X + 1, slot.Y + 1, slot.Width - 2, slot.Height - 2);
            Color color = FillColor(progress.Phase);

            using (SolidBrush trackBrush = new SolidBrush(Track))
            {
                graphics.FillRectangle(trackBrush, slot);
            }

            bool unknownTotal = progress.IsRunning && !progress.Fraction.HasValue;
            Rectangle fill = Rectangle.Empty;

            if (progress.Phase == CopyPhase.Finished || progress.Phase == CopyPhase.Failed ||
                progress.Phase == CopyPhase.Stopped)
            {
                fill = inner;
            }
            else if (progress.IsRunning && progress.Fraction.HasValue && progress.Fraction.Value > 0)
            {
                int width = (int)Math.Round(inner.Width * progress.Fraction.Value);
                fill = new Rectangle(inner.X, inner.Y, Math.Max(2, Math.Min(width, inner.Width)), inner.Height);
            }

            if (fill.Width > 0)
            {
                using (SolidBrush brush = new SolidBrush(color))
                {
                    graphics.FillRectangle(brush, fill);
                }
            }
            else if (unknownTotal)
            {
                // Copying, but the total is not known yet: stripes rather than an invented length.
                using (HatchBrush stripes = new HatchBrush(HatchStyle.WideUpwardDiagonal, color, Color.FromArgb(214, 227, 246)))
                {
                    graphics.FillRectangle(stripes, inner);
                }
            }

            using (Pen border = new Pen(TrackBorder))
            {
                graphics.DrawRectangle(border, slot.X, slot.Y, slot.Width - 1, slot.Height - 1);
            }

            string text = progress.BarText;
            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

            if (unknownTotal)
            {
                // A plain plate behind the words keeps them readable over the stripes.
                Size size = TextRenderer.MeasureText(graphics, text, font);
                Rectangle plate = new Rectangle(slot.X + (slot.Width - size.Width) / 2 - 3, slot.Y + 2,
                                                size.Width + 6, slot.Height - 4);
                plate.Intersect(inner);
                using (SolidBrush plateBrush = new SolidBrush(Color.FromArgb(244, 248, 253)))
                {
                    graphics.FillRectangle(plateBrush, plate);
                }
            }

            TextRenderer.DrawText(graphics, text, font, slot, Color.Black, flags);
        }
    }
}
