using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CCJ.Models
{
    public enum CopyPhase
    {
        /// <summary>The run has started but robocopy has not (checking the shares, or waiting to retry).</summary>
        Waiting,

        /// <summary>robocopy is running and writing its log.</summary>
        Copying,

        /// <summary>The script logged SUCCESS.</summary>
        Finished,

        /// <summary>The script logged FAILED after all its attempts.</summary>
        Failed,

        /// <summary>The task ended without logging a result, for example because it was stopped.</summary>
        Stopped,

        /// <summary>robocopy is suspended, waiting for Resume. The run is still alive.</summary>
        Paused
    }

    /// <summary>Human readable sizes and durations, shared by the grid, the dialogs and the tests.</summary>
    public static class SizeFormat
    {
        public static string Bytes(long bytes)
        {
            if (bytes < 0) bytes = 0;
            if (bytes < 1024) return bytes.ToString(CultureInfo.CurrentCulture) + " B";

            string[] units = new string[] { "KB", "MB", "GB", "TB", "PB" };
            double value = bytes;
            int unit = -1;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            string format = value >= 100 ? "0" : (value >= 10 ? "0.#" : "0.##");
            return value.ToString(format, CultureInfo.CurrentCulture) + " " + units[unit];
        }

        public static string Duration(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            if (span.TotalHours >= 1)
                return string.Format(CultureInfo.InvariantCulture, "{0}h {1:00}m", (int)span.TotalHours, span.Minutes);
            if (span.TotalMinutes >= 1)
                return string.Format(CultureInfo.InvariantCulture, "{0}m {1:00}s", (int)span.TotalMinutes, span.Seconds);
            return ((int)span.TotalSeconds).ToString(CultureInfo.InvariantCulture) + "s";
        }
    }

    /// <summary>
    /// One snapshot of how far a backup run has got. Built by CopyProgressMonitor, shown by the
    /// dashboard grid and the detail line under it.
    ///
    /// "Done" counts files whose copy has finished. The file robocopy is working on right now is
    /// reported separately by name, because robocopy gives no way to see how much of it is written.
    /// </summary>
    public class CopyProgress
    {
        public string Key { get; set; }
        public bool IsDryRun { get; set; }
        public CopyPhase Phase { get; set; }

        /// <summary>The last problem while waiting, or why the run failed or stopped.</summary>
        public string Message { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public TimeSpan Elapsed { get; set; }

        public long FilesDone { get; set; }
        public long? TotalFiles { get; set; }
        public long BytesDone { get; set; }
        public long? TotalBytes { get; set; }

        public string CurrentFile { get; set; }
        public long CurrentFileBytes { get; set; }
        public TimeSpan CurrentFileElapsed { get; set; }

        /// <summary>Timestamped error lines in the robocopy log. robocopy retries these on its own.</summary>
        public int ErrorLines { get; set; }

        public int? Attempt { get; set; }
        public int? MaxAttempts { get; set; }

        public double? BytesPerSecond { get; set; }
        public TimeSpan? Remaining { get; set; }

        public string LogFile { get; set; }

        /// <summary>True while the list-only pass that works out the total is still running.</summary>
        public bool Planning { get; set; }

        /// <summary>Why the total is unknown, when it is.</summary>
        public string PlanError { get; set; }

        /// <summary>A paused run is still a run: it is only waiting to be resumed.</summary>
        public bool IsActive
        {
            get
            {
                return Phase == CopyPhase.Waiting || Phase == CopyPhase.Copying ||
                       Phase == CopyPhase.Paused;
            }
        }

        /// <summary>True while robocopy exists and can be paused, resumed or stopped.</summary>
        public bool IsRunning
        {
            get { return Phase == CopyPhase.Copying || Phase == CopyPhase.Paused; }
        }

        /// <summary>
        /// 0..1 when the total is known, otherwise null. Never reaches 1 until the script reports
        /// SUCCESS, so a bar at 100% always means the run is really over.
        /// </summary>
        public double? Fraction
        {
            get
            {
                if (Phase == CopyPhase.Finished) return 1.0;

                double? value = null;
                if (TotalBytes.HasValue && TotalBytes.Value > 0)
                    value = BytesDone / (double)TotalBytes.Value;
                else if (TotalFiles.HasValue && TotalFiles.Value > 0)
                    value = FilesDone / (double)TotalFiles.Value;
                else if (TotalFiles.HasValue && TotalFiles.Value == 0)
                    value = 0.99; // nothing needed copying, the run is about to report its result

                if (!value.HasValue) return null;
                return Math.Max(0.0, Math.Min(0.99, value.Value));
            }
        }

        public int Percent
        {
            get
            {
                double? fraction = Fraction;
                return fraction.HasValue ? (int)Math.Floor(fraction.Value * 100.0) : 0;
            }
        }

        /// <summary>The short text drawn inside the progress bar.</summary>
        public string BarText
        {
            get
            {
                string prefix = IsDryRun ? "Dry run · " : "";

                switch (Phase)
                {
                    case CopyPhase.Waiting:
                        if (Attempt.HasValue && MaxAttempts.HasValue)
                            return prefix + "Waiting · attempt " + Attempt.Value + "/" + MaxAttempts.Value;
                        return prefix + "Starting...";

                    case CopyPhase.Copying:
                        if (Fraction.HasValue)
                        {
                            string files = FilesDone.ToString("N0", CultureInfo.CurrentCulture);
                            if (TotalFiles.HasValue)
                                files += "/" + TotalFiles.Value.ToString("N0", CultureInfo.CurrentCulture);
                            return prefix + Percent + "% · " + files + " files";
                        }
                        return prefix + FilesDone.ToString("N0", CultureInfo.CurrentCulture) + " files" +
                               (Planning ? " · counting..." : "");

                    case CopyPhase.Paused:
                        if (Fraction.HasValue)
                            return prefix + "Paused · " + Percent + "%";
                        return prefix + "Paused · " +
                               FilesDone.ToString("N0", CultureInfo.CurrentCulture) + " files";

                    case CopyPhase.Finished:
                        if (FilesDone == 0) return prefix + "Done · nothing to copy";
                        return prefix + "Done · " + FilesDone.ToString("N0", CultureInfo.CurrentCulture) + " files";

                    case CopyPhase.Failed:
                        return prefix + "Failed";

                    default:
                        return prefix + "Stopped";
                }
            }
        }

        /// <summary>Several lines for the label under the grid.</summary>
        public string Detail
        {
            get
            {
                StringBuilder text = new StringBuilder();

                text.Append(Key);
                if (IsDryRun) text.Append(" (dry run)");
                text.Append(" - ").Append(PhaseText());
                text.Append("   |   elapsed ").Append(SizeFormat.Duration(Elapsed));
                if (!string.IsNullOrEmpty(LogFile))
                    text.Append("   |   log ").Append(Path.GetFileName(LogFile));
                text.AppendLine();

                if (Phase != CopyPhase.Waiting)
                {
                    text.Append(FilesDone.ToString("N0", CultureInfo.CurrentCulture));
                    if (TotalFiles.HasValue) text.Append(" of ").Append(TotalFiles.Value.ToString("N0", CultureInfo.CurrentCulture));
                    text.Append(" files");

                    text.Append("   |   ").Append(SizeFormat.Bytes(BytesDone));
                    if (TotalBytes.HasValue) text.Append(" of ").Append(SizeFormat.Bytes(TotalBytes.Value));

                    if (BytesPerSecond.HasValue && Phase == CopyPhase.Copying)
                        text.Append("   |   ").Append(SizeFormat.Bytes((long)BytesPerSecond.Value)).Append("/s");
                    if (Remaining.HasValue && Phase == CopyPhase.Copying)
                        text.Append("   |   about ").Append(SizeFormat.Duration(Remaining.Value)).Append(" left");
                    text.AppendLine();
                }

                if ((Phase == CopyPhase.Copying || Phase == CopyPhase.Paused) &&
                    !string.IsNullOrEmpty(CurrentFile))
                {
                    text.Append("Now copying: ").Append(CurrentFile)
                        .Append(" (").Append(SizeFormat.Bytes(CurrentFileBytes)).Append(")")
                        .Append(" for ").Append(SizeFormat.Duration(CurrentFileElapsed)).AppendLine();
                }
                else if (!string.IsNullOrEmpty(Message))
                {
                    text.Append(Message).AppendLine();
                }

                if (Phase == CopyPhase.Paused)
                {
                    text.Append("Paused: robocopy is suspended, so nothing is being written. ")
                        .Append("Use Resume to carry on, or Stop to end the run.").AppendLine();
                }

                if (ErrorLines > 0)
                {
                    text.Append(ErrorLines).Append(" error line(s) in the robocopy log. robocopy retries on its own ")
                        .Append("(10 times, 30 seconds apart) before giving up on a file.").AppendLine();
                }
                if (Phase == CopyPhase.Copying && !TotalFiles.HasValue)
                {
                    if (Planning)
                        text.Append("Counting what has to be copied, so the percentage appears shortly.").AppendLine();
                    else if (!string.IsNullOrEmpty(PlanError))
                        text.Append("Total unknown: ").Append(PlanError).AppendLine();
                }

                return text.ToString().TrimEnd();
            }
        }

        private string PhaseText()
        {
            switch (Phase)
            {
                case CopyPhase.Waiting: return "waiting";
                case CopyPhase.Copying: return "copying";
                case CopyPhase.Paused: return "paused";
                case CopyPhase.Finished: return "finished";
                case CopyPhase.Failed: return "failed";
                default: return "stopped";
            }
        }

        public override string ToString()
        {
            return BarText;
        }
    }
}
