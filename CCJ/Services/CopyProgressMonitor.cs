using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CCJ.Models;

namespace CCJ.Services
{
    /// <summary>Describes one run to follow.</summary>
    public class TrackedRun
    {
        public TrackedRun()
        {
            Grace = TimeSpan.FromSeconds(20);
        }

        public JobConfig Job { get; set; }

        /// <summary>The folder that holds master.log and the per run robocopy logs.</summary>
        public string LogsDir { get; set; }

        /// <summary>When the run began. Only logs created after this belong to it.</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Size of master.log when the run was started, so older runs are ignored. Null when
        /// attaching to a run that is already under way: the last "job started" line is used.
        /// </summary>
        public long? MasterBaseline { get; set; }

        public bool IsDryRun { get; set; }

        /// <summary>Asks whether the run is still alive. Null means unknown.</summary>
        public Func<bool> IsRunning { get; set; }

        /// <summary>
        /// Asks whether the user has paused this run, so a copy that is suspended is reported as
        /// paused instead of looking like a copy that has stalled. Null means never paused.
        /// </summary>
        public Func<bool> IsPaused { get; set; }

        /// <summary>Time allowed for the run to show signs of life before "not running" counts.</summary>
        public TimeSpan Grace { get; set; }

        /// <summary>A total worked out before the copy started, for example while the confirmation was open.</summary>
        public Task<PlanResult> PrePlan { get; set; }
        public CancellationTokenSource PrePlanSource { get; set; }

        /// <summary>Starts a list-only pass. Null disables planning, which the unit tests use.</summary>
        public Func<CancellationToken, Task<PlanResult>> PlanFactory { get; set; }
    }

    internal enum FinalState
    {
        None,
        Success,
        Failed
    }

    internal class MasterState
    {
        public bool Started;
        public int? Attempt;
        public int? MaxAttempts;
        public string Problem;
        public FinalState Final;
        public string FinalMessage;
        public string LogHint;
    }

    /// <summary>
    /// Follows one backup run and turns what it leaves behind into a <see cref="CopyProgress"/>.
    ///
    /// It never talks to robocopy. It reads two files the script already writes:
    ///
    ///   master.log        "job started", "Attempt N of M", "Problem: ...", and finally
    ///                     "SUCCESS:" or "FAILED:". These are the script's own English words, so
    ///                     they do not depend on the Windows language.
    ///   FOLDER_stamp.log  robocopy's log. A line is written when a file starts to copy, so the
    ///                     newest file line is the file in flight, and every earlier one is done.
    ///
    /// The total comes from <see cref="RobocopyPlanner"/>, a list-only pass.
    ///
    /// Not thread safe: one thread polls a monitor.
    /// </summary>
    public class CopyProgressMonitor : IDisposable
    {
        private const int TailBytes = 1024 * 1024;
        private const int MaxPlanAttempts = 4;
        private static readonly TimeSpan PlanRetryDelay = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan ProbeEvery = TimeSpan.FromSeconds(10);

        private static readonly Regex AttemptRegex = new Regex(
            @"Attempt (\d+) of (\d+)", RegexOptions.Compiled);

        // "... Log: C:\Vox\Logs\NADIR_2026-11-22_00-00-05.log" or "..., see C:\...\NADIR_....log."
        private static readonly Regex LogHintRegex = new Regex(
            @"(?:Log: |see )(.+?\.log)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly TrackedRun _run;
        private readonly HashSet<string> _claims;
        private readonly string _folder;
        private readonly string _masterPath;
        private readonly Regex _logNameRegex;
        private readonly Encoding _encoding = ScriptGenerator.BatchEncoding;

        private string _logPath;
        private IncrementalLineReader _reader;

        // Counts for the current robocopy attempt, plus what earlier attempts of the same run got done.
        private long _filesDone;
        private long _bytesDone;
        private long _carryFiles;
        private long _carryBytes;
        private int _errors;
        private int _carryErrors;
        private string _currentName;
        private long _currentBytes;
        private DateTime _currentSince;
        private DateTime? _copyStartedAt;
        private bool _flushed;

        private Task<PlanResult> _planTask;
        private CancellationTokenSource _planSource;
        private PlanResult _plan;
        private long _planBaseFiles;
        private long _planBaseBytes;
        private string _planError;
        private int _planAttempts;
        private DateTime _planStartedAt;

        private DateTime? _lastProbe;
        private bool _lastProbeStopped;

        private CopyProgress _last;
        private bool _disposed;

        public CopyProgressMonitor(TrackedRun run, HashSet<string> claims)
        {
            if (run == null) throw new ArgumentNullException("run");
            if (run.Job == null) throw new ArgumentException("A job is required.", "run");

            _run = run;
            _claims = claims ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _folder = (run.Job.NasFolder ?? "").Trim();
            _masterPath = Path.Combine(run.LogsDir ?? "", "master.log");

            // The script names its log FOLDER_yyyy-MM-dd_HH-mm-ss.log.
            _logNameRegex = new Regex(
                "^" + Regex.Escape(_folder) + @"_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}\.log$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            if (run.PrePlan != null)
            {
                _planTask = run.PrePlan;
                _planSource = run.PrePlanSource;
                _planAttempts = 1;
                _planStartedAt = run.StartedAt;
            }
        }

        public string Key { get { return _run.Job.Key; } }
        public bool IsComplete { get; private set; }
        public CopyProgress Last { get { return _last; } }
        public string LogPath { get { return _logPath; } }

        /// <summary>The folder this run's logs are written to, which tells a dry run from a real one.</summary>
        public string LogsDir { get { return _run.LogsDir; } }

        /// <summary>The NAS folder name, which is also the prefix of this run's robocopy log.</summary>
        public string Folder { get { return _folder; } }

        public bool IsDryRun { get { return _run.IsDryRun; } }

        // ------------------------------------------------------------------ polling

        public CopyProgress Poll(DateTime now)
        {
            if (IsComplete && _last != null) return _last;

            MasterState master = ReadMaster();

            if (_logPath == null)
            {
                if (master.LogHint != null && File.Exists(master.LogHint))
                    AdoptLog(master.LogHint, false);
                else if (master.Final == FinalState.None)
                    FindLog();
            }

            if (_logPath != null)
                ReadLog(now);

            if (master.Final == FinalState.Success)
                FlushInFlight();

            UpdatePlan(now);

            CopyProgress snapshot = BuildSnapshot(now, master);
            _last = snapshot;

            if (IsComplete) Release();
            return snapshot;
        }

        // ------------------------------------------------------------------ master.log

        private MasterState ReadMaster()
        {
            MasterState state = new MasterState();
            string[] lines = ReadMasterLines();
            if (lines.Length == 0) return state;

            string startedMarker = "===== " + _folder + " job started";
            int first = 0;

            if (!_run.MasterBaseline.HasValue)
            {
                // Attaching to a run that is already going: its section starts at the last
                // "job started" line for this folder, so earlier runs cannot be mistaken for it.
                first = -1;
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    if (lines[i].IndexOf(startedMarker, StringComparison.Ordinal) >= 0)
                    {
                        first = i;
                        break;
                    }
                }
                if (first < 0) return state;
            }

            string successMarker = "SUCCESS: " + _folder + " ";
            string failedMarker = "FAILED: " + _folder + " ";

            for (int i = first; i < lines.Length; i++)
            {
                string line = lines[i];

                if (line.IndexOf(startedMarker, StringComparison.Ordinal) >= 0)
                {
                    state.Started = true;
                    continue;
                }

                Match attempt = AttemptRegex.Match(line);
                if (attempt.Success)
                {
                    state.Attempt = int.Parse(attempt.Groups[1].Value, CultureInfo.InvariantCulture);
                    state.MaxAttempts = int.Parse(attempt.Groups[2].Value, CultureInfo.InvariantCulture);
                    continue;
                }

                int problem = line.IndexOf("Problem: ", StringComparison.Ordinal);
                if (problem >= 0)
                {
                    state.Problem = line.Substring(problem + "Problem: ".Length).Trim();
                    Match hint = LogHintRegex.Match(state.Problem);
                    if (hint.Success) state.LogHint = hint.Groups[1].Value;
                    continue;
                }

                if (line.IndexOf(successMarker, StringComparison.Ordinal) >= 0)
                {
                    state.Final = FinalState.Success;
                    Match hint = LogHintRegex.Match(line);
                    if (hint.Success) state.LogHint = hint.Groups[1].Value;
                    continue;
                }

                int failed = line.IndexOf(failedMarker, StringComparison.Ordinal);
                if (failed >= 0)
                {
                    state.Final = FinalState.Failed;
                    int last = line.IndexOf("Last problem: ", StringComparison.Ordinal);
                    state.FinalMessage = last >= 0
                        ? line.Substring(last + "Last problem: ".Length).Trim()
                        : line.Substring(failed).Trim();
                }
            }

            return state;
        }

        private string[] ReadMasterLines()
        {
            if (!File.Exists(_masterPath)) return new string[0];

            try
            {
                using (FileStream stream = new FileStream(
                    _masterPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long start = 0;
                    bool partialFirstLine = false;

                    if (_run.MasterBaseline.HasValue)
                    {
                        // A log shorter than the baseline was replaced; read it all.
                        start = _run.MasterBaseline.Value <= stream.Length ? _run.MasterBaseline.Value : 0;
                    }
                    else if (stream.Length > TailBytes)
                    {
                        start = stream.Length - TailBytes;
                        partialFirstLine = true;
                    }

                    stream.Seek(start, SeekOrigin.Begin);
                    byte[] buffer = new byte[stream.Length - start];
                    int total = 0;
                    while (total < buffer.Length)
                    {
                        int read = stream.Read(buffer, total, buffer.Length - total);
                        if (read <= 0) break;
                        total += read;
                    }

                    string text = _encoding.GetString(buffer, 0, total);
                    List<string> lines = new List<string>(
                        text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries));

                    if (partialFirstLine && lines.Count > 0) lines.RemoveAt(0);
                    return lines.ToArray();
                }
            }
            catch (IOException)
            {
                return new string[0];
            }
            catch (UnauthorizedAccessException)
            {
                return new string[0];
            }
        }

        // ------------------------------------------------------------------ the robocopy log

        private void FindLog()
        {
            if (string.IsNullOrEmpty(_run.LogsDir) || !Directory.Exists(_run.LogsDir)) return;

            // A run's log is created after the run starts, never before. Allowing any slack here
            // would let a run that follows closely on another one adopt the previous run's log.
            DateTime threshold = _run.StartedAt;
            string best = null;
            DateTime bestTime = DateTime.MaxValue;

            string[] candidates;
            try
            {
                candidates = Directory.GetFiles(_run.LogsDir, _folder + "_*.log");
            }
            catch (IOException)
            {
                return;
            }

            foreach (string path in candidates)
            {
                if (!_logNameRegex.IsMatch(Path.GetFileName(path))) continue;

                DateTime created = File.GetCreationTime(path);
                if (created < threshold) continue;

                lock (_claims)
                {
                    if (_claims.Contains(path)) continue;
                }

                // The earliest log from this run onwards. When two jobs share a folder name the
                // claims keep them from picking the same file.
                if (created < bestTime)
                {
                    best = path;
                    bestTime = created;
                }
            }

            if (best != null) AdoptLog(best, true);
        }

        private void AdoptLog(string path, bool claim)
        {
            if (claim)
            {
                lock (_claims)
                {
                    if (_claims.Contains(path)) return;
                    _claims.Add(path);
                }
            }

            _logPath = path;
            _reader = new IncrementalLineReader(path, _encoding);
        }

        private void ReadLog(DateTime now)
        {
            List<string> lines;
            try
            {
                lines = _reader.ReadNewLines();
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            if (_reader.WasReset) StartNewAttempt();

            foreach (string raw in lines)
            {
                ParsedLogLine line = RobocopyLogParser.Parse(raw);

                if (line.Kind == LogLineKind.Error)
                {
                    _errors++;
                }
                else if (line.Kind == LogLineKind.File)
                {
                    if (!_copyStartedAt.HasValue) _copyStartedAt = now;

                    // A new file line means the previous file is finished.
                    if (_currentName != null)
                    {
                        _filesDone++;
                        _bytesDone += _currentBytes;
                    }

                    _currentName = line.Name;
                    _currentBytes = line.Size;
                    _currentSince = now;
                }
            }
        }

        /// <summary>
        /// The script reuses one log name for each attempt, and robocopy starts it afresh. What
        /// earlier attempts finished stays counted, so the bar does not jump back.
        /// </summary>
        private void StartNewAttempt()
        {
            _carryFiles += _filesDone;
            _carryBytes += _bytesDone;
            _carryErrors += _errors;
            _filesDone = 0;
            _bytesDone = 0;
            _errors = 0;
            _currentName = null;
            _currentBytes = 0;
            _flushed = false;
        }

        /// <summary>On SUCCESS the file in flight has finished too.</summary>
        private void FlushInFlight()
        {
            if (_flushed) return;
            _flushed = true;

            if (_currentName != null)
            {
                _filesDone++;
                _bytesDone += _currentBytes;
                _currentName = null;
                _currentBytes = 0;
            }
        }

        // ------------------------------------------------------------------ the total

        private long FilesDoneNow { get { return _carryFiles + _filesDone; } }
        private long BytesDoneNow { get { return _carryBytes + _bytesDone; } }

        private void UpdatePlan(DateTime now)
        {
            if (_plan != null) return;

            if (_planTask != null)
            {
                if (!_planTask.IsCompleted) return;

                PlanResult result = _planTask.Status == TaskStatus.RanToCompletion ? _planTask.Result : null;
                if (result != null && result.Complete)
                {
                    _plan = result;
                    _planError = null;
                }
                else
                {
                    _planError = result != null && !string.IsNullOrEmpty(result.Error)
                        ? result.Error
                        : "the list-only check failed.";
                    _planTask = null;
                }
                return;
            }

            if (_run.PlanFactory == null) return;

            // For a run attached to mid way, wait until robocopy is actually running: before that
            // the shares are usually unreachable and the pass would only fail.
            if (_logPath == null) return;
            if (_planAttempts >= MaxPlanAttempts) return;
            if (_planAttempts > 0 && now - _planStartedAt < PlanRetryDelay) return;

            // What is already done is not in the new pass, because it has been copied since.
            _planBaseFiles = FilesDoneNow;
            _planBaseBytes = BytesDoneNow;
            _planSource = new CancellationTokenSource();
            _planTask = _run.PlanFactory(_planSource.Token);
            _planStartedAt = now;
            _planAttempts++;
        }

        // ------------------------------------------------------------------ the snapshot

        private CopyProgress BuildSnapshot(DateTime now, MasterState master)
        {
            CopyProgress p = new CopyProgress();
            p.Key = _run.Job.Key;
            p.IsDryRun = _run.IsDryRun;
            p.StartedAt = _run.StartedAt;
            p.UpdatedAt = now;
            p.Elapsed = now - _run.StartedAt;
            p.Attempt = master.Attempt;
            p.MaxAttempts = master.MaxAttempts;
            p.LogFile = _logPath;
            p.ErrorLines = _carryErrors + _errors;

            p.FilesDone = FilesDoneNow;
            p.BytesDone = BytesDoneNow;

            if (_plan != null)
            {
                p.TotalFiles = _planBaseFiles + _plan.Files;
                p.TotalBytes = _planBaseBytes + _plan.Bytes;
            }
            p.Planning = _plan == null && _planTask != null && !_planTask.IsCompleted;
            p.PlanError = _plan == null ? _planError : null;

            if (_currentName != null)
            {
                p.CurrentFile = _currentName;
                p.CurrentFileBytes = _currentBytes;
                p.CurrentFileElapsed = now - _currentSince;
            }

            if (master.Final == FinalState.Success)
            {
                p.Phase = CopyPhase.Finished;
                IsComplete = true;
            }
            else if (master.Final == FinalState.Failed)
            {
                p.Phase = CopyPhase.Failed;
                p.Message = master.FinalMessage;
                IsComplete = true;
            }
            else if (IsStopped(now))
            {
                p.Phase = CopyPhase.Stopped;
                p.Message = "The task is no longer running and did not report a result. It may have been stopped.";
                IsComplete = true;
            }
            else if (_logPath != null)
            {
                p.Phase = CopyPhase.Copying;
            }
            else
            {
                p.Phase = CopyPhase.Waiting;
                p.Message = master.Started && !string.IsNullOrEmpty(master.Problem)
                    ? master.Problem
                    : "Starting. Checking that the source and the NAS can be reached.";
            }

            if (p.Phase == CopyPhase.Copying && IsPausedNow())
            {
                // Suspended on purpose. The counts stay as they are; nothing is moving.
                p.Phase = CopyPhase.Paused;
            }
            else if (p.Phase == CopyPhase.Copying)
            {
                AddRate(p, now);
            }

            return p;
        }

        private bool IsPausedNow()
        {
            if (_run.IsPaused == null) return false;
            try
            {
                return _run.IsPaused();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool IsStopped(DateTime now)
        {
            if (_run.IsRunning == null) return false;
            if (now - _run.StartedAt <= _run.Grace) return false;

            // Asking Task Scheduler is slower than reading a file, and the answer rarely changes,
            // so it is asked at most every ten seconds.
            if (_lastProbe.HasValue && now - _lastProbe.Value < ProbeEvery) return _lastProbeStopped;

            try
            {
                _lastProbeStopped = !_run.IsRunning();
            }
            catch (Exception)
            {
                // If the probe itself fails we cannot tell, so assume the run is alive.
                _lastProbeStopped = false;
            }

            _lastProbe = now;
            return _lastProbeStopped;
        }

        /// <summary>
        /// Speed is what has finished divided by the time spent copying. The file in flight is not
        /// counted until it ends, so early in a very large file the speed reads low. That is the
        /// honest direction to be wrong in.
        /// </summary>
        private void AddRate(CopyProgress p, DateTime now)
        {
            if (!_copyStartedAt.HasValue) return;

            double seconds = (now - _copyStartedAt.Value).TotalSeconds;
            if (seconds < 10 || p.BytesDone <= 0) return;

            double rate = p.BytesDone / seconds;
            if (rate <= 0) return;

            p.BytesPerSecond = rate;
            if (p.TotalBytes.HasValue && p.TotalBytes.Value > p.BytesDone)
                p.Remaining = TimeSpan.FromSeconds((p.TotalBytes.Value - p.BytesDone) / rate);
        }

        // ------------------------------------------------------------------ cleanup

        private void Release()
        {
            if (_planSource != null)
            {
                try
                {
                    _planSource.Cancel();
                }
                catch (Exception)
                {
                }
            }

            if (_logPath != null)
            {
                lock (_claims)
                {
                    _claims.Remove(_logPath);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Release();
        }
    }
}
