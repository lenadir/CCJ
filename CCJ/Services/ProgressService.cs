using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CCJ.Models;

namespace CCJ.Services
{
    /// <summary>What Task Scheduler says about one job's task.</summary>
    public class TaskRunInfo
    {
        public TaskRunInfo(bool running, DateTime? startedAt)
        {
            Running = running;
            StartedAt = startedAt;
        }

        public bool Running { get; private set; }

        /// <summary>When the current run started, if it is running.</summary>
        public DateTime? StartedAt { get; private set; }
    }

    public class ProgressEventArgs : EventArgs
    {
        public ProgressEventArgs(CopyProgress progress)
        {
            Progress = progress;
        }

        public CopyProgress Progress { get; private set; }
    }

    /// <summary>
    /// Keeps one <see cref="CopyProgressMonitor"/> per job, polls them on a background timer and
    /// tells the form when something changed. It also notices a task that is already running, for
    /// example a scheduled run that started while CCJ was closed, and attaches to it.
    ///
    /// Everything slow (reading files, asking Task Scheduler, running the list-only pass) happens
    /// on the timer thread or the thread pool, never on the UI thread.
    /// </summary>
    public class ProgressService : IDisposable
    {
        /// <summary>Look for tasks that are running every this many polls.</summary>
        private const int DiscoverEvery = 5;

        /// <summary>A run that just finished is not picked up again for this long.</summary>
        private static readonly TimeSpan RediscoverDelay = TimeSpan.FromSeconds(90);

        /// <summary>A total prepared while the confirmation was open is only trusted this long.</summary>
        private static readonly TimeSpan PlanMaxAge = TimeSpan.FromMinutes(30);

        private class PlanEntry
        {
            public Task<PlanResult> Result;
            public CancellationTokenSource Source;
            public DateTime CreatedAt;
        }

        private readonly Func<JobsFile> _settings;
        private readonly Func<JobConfig, TaskRunInfo> _probe;
        private readonly RobocopyPlanner _planner;

        private readonly object _gate = new object();
        private readonly Dictionary<string, CopyProgressMonitor> _monitors =
            new Dictionary<string, CopyProgressMonitor>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CopyProgress> _latest =
            new Dictionary<string, CopyProgress>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _finishedAt =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PlanEntry> _plans =
            new Dictionary<string, PlanEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _claims = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _paused = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private Timer _timer;
        private int _busy;
        private int _tick;
        private int _discoverRequested;
        private bool _disposed;
        private bool _probeFailureLogged;

        public ProgressService(Func<JobsFile> settings, Func<JobConfig, TaskRunInfo> probe)
            : this(settings, probe, new RobocopyPlanner())
        {
        }

        public ProgressService(Func<JobsFile> settings, Func<JobConfig, TaskRunInfo> probe, RobocopyPlanner planner)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            _settings = settings;
            _probe = probe;
            _planner = planner;
        }

        /// <summary>Raised on a background thread. The form must hand the update to the UI thread.</summary>
        public event EventHandler<ProgressEventArgs> Changed;

        public void Start(int intervalMs)
        {
            lock (_gate)
            {
                if (_timer != null || _disposed) return;
                _timer = new Timer(new TimerCallback(OnTick), null, 0, intervalMs);
            }
        }

        // ------------------------------------------------------------------ what the form asks for

        /// <summary>The newest snapshot for a job, or null if nothing has been tracked.</summary>
        public CopyProgress Latest(string key)
        {
            lock (_gate)
            {
                CopyProgress progress;
                return _latest.TryGetValue(key, out progress) ? progress : null;
            }
        }

        public bool IsActive(string key)
        {
            lock (_gate)
            {
                CopyProgressMonitor monitor;
                return _monitors.TryGetValue(key, out monitor) && !monitor.IsComplete;
            }
        }

        /// <summary>
        /// Where this run's robocopy log is, as soon as the run has one. It identifies the robocopy
        /// process to pause, resume or stop, and tells a dry run apart from a real run.
        /// </summary>
        public string ActiveLogPath(string key)
        {
            lock (_gate)
            {
                CopyProgressMonitor monitor;
                return _monitors.TryGetValue(key, out monitor) ? monitor.LogPath : null;
            }
        }

        /// <summary>The folder this run logs into, which is under TEMP for a dry run.</summary>
        public string ActiveLogsDir(string key)
        {
            lock (_gate)
            {
                CopyProgressMonitor monitor;
                return _monitors.TryGetValue(key, out monitor) ? monitor.LogsDir : null;
            }
        }

        /// <summary>The NAS folder name of the run being followed, the prefix of its log name.</summary>
        public string ActiveFolder(string key)
        {
            lock (_gate)
            {
                CopyProgressMonitor monitor;
                return _monitors.TryGetValue(key, out monitor) ? monitor.Folder : null;
            }
        }

        /// <summary>True when the run being followed is a dry run.</summary>
        public bool ActiveIsDryRun(string key)
        {
            lock (_gate)
            {
                CopyProgressMonitor monitor;
                return _monitors.TryGetValue(key, out monitor) && monitor.IsDryRun;
            }
        }

        /// <summary>
        /// Records that the user paused or resumed this job, so the next snapshot says "paused"
        /// rather than looking like a copy that has gone quiet. It does not suspend anything
        /// itself: <see cref="CopyControl"/> does that.
        /// </summary>
        public void SetPaused(string key, bool paused)
        {
            lock (_gate)
            {
                if (paused) _paused.Add(key);
                else _paused.Remove(key);
            }

            // Show it at once instead of at the next tick.
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate { RunPoll(); }));
        }

        public bool IsPaused(string key)
        {
            lock (_gate)
            {
                return _paused.Contains(key);
            }
        }

        /// <summary>
        /// Starts working out the total while the confirmation is open, so it is ready by the time
        /// the copy starts. Read only. Cancel it if the user says no.
        /// </summary>
        public Task<PlanResult> PlanAhead(JobConfig job)
        {
            CancelPlan(job.Key);
            if (_planner == null)
            {
                TaskCompletionSource<PlanResult> none = new TaskCompletionSource<PlanResult>();
                none.SetResult(new PlanResult { Error = "planning is switched off" });
                return none.Task;
            }

            PlanEntry entry = new PlanEntry();
            entry.Source = new CancellationTokenSource();
            entry.CreatedAt = DateTime.Now;
            entry.Result = _planner.PlanAsync(job.SourcePath, job.DestinationPath, entry.Source.Token);

            lock (_gate)
            {
                _plans[job.Key] = entry;
            }
            return entry.Result;
        }

        public void CancelPlan(string key)
        {
            PlanEntry entry = null;
            lock (_gate)
            {
                if (_plans.TryGetValue(key, out entry)) _plans.Remove(key);
            }
            Cancel(entry);
        }

        /// <summary>
        /// Call just before starting the task. It remembers how long master.log is now, so only
        /// what the new run writes is read.
        /// </summary>
        public void TrackTask(JobConfig job)
        {
            JobsFile settings = _settings();
            TrackedRun run = NewRun(job, settings.LogsDir, DateTime.Now, false);
            run.MasterBaseline = LengthOf(settings.MasterLogPath);
            run.IsRunning = TaskProbe(job);
            Attach(run);
        }

        /// <summary>For the dry run, which CCJ starts itself in a folder under TEMP.</summary>
        public void TrackProcess(JobConfig job, string logsDir, Func<bool> isRunning)
        {
            TrackedRun run = NewRun(job, logsDir, DateTime.Now, true);
            run.MasterBaseline = LengthOf(Path.Combine(logsDir, "master.log"));
            run.IsRunning = isRunning;
            Attach(run);
        }

        /// <summary>Attaches to a task that is already running.</summary>
        public void TrackRunning(JobConfig job, DateTime? startedAt)
        {
            JobsFile settings = _settings();
            TrackedRun run = NewRun(job, settings.LogsDir, startedAt ?? DateTime.Now, false);
            run.MasterBaseline = null;
            run.IsRunning = TaskProbe(job);
            Attach(run);
        }

        public void Untrack(string key)
        {
            CopyProgressMonitor old = null;
            lock (_gate)
            {
                if (_monitors.TryGetValue(key, out old)) _monitors.Remove(key);
                _latest.Remove(key);
                _paused.Remove(key);
            }
            if (old != null) old.Dispose();
        }

        /// <summary>Looks for running tasks now instead of waiting for the next scheduled look.</summary>
        public void DiscoverNow()
        {
            Interlocked.Exchange(ref _discoverRequested, 1);
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate { RunPoll(); }));
        }

        // ------------------------------------------------------------------ polling

        private void OnTick(object state)
        {
            RunPoll();
        }

        /// <summary>
        /// One look at every active run. Public so the tests can drive it without a timer. Unlike
        /// the timer, which skips a tick when the last one is still running, a caller that asks
        /// for a poll waits for its turn, so it always gets a complete one.
        /// </summary>
        public void PollOnce()
        {
            while (!_disposed && Interlocked.Exchange(ref _busy, 1) == 1)
                Thread.Sleep(5);

            if (_disposed) return;
            PollBody();
        }

        private void RunPoll()
        {
            if (_disposed) return;
            if (Interlocked.Exchange(ref _busy, 1) == 1) return;

            PollBody();
        }

        /// <summary>The work of one poll. The caller has already taken the busy flag.</summary>
        private void PollBody()
        {
            try
            {
                int tick = Interlocked.Increment(ref _tick);
                bool requested = Interlocked.Exchange(ref _discoverRequested, 0) == 1;
                if (requested || tick % DiscoverEvery == 1) Discover();

                List<CopyProgressMonitor> active;
                lock (_gate)
                {
                    active = new List<CopyProgressMonitor>(_monitors.Values);
                }

                foreach (CopyProgressMonitor monitor in active)
                {
                    if (monitor.IsComplete) continue;
                    Publish(monitor);
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("Reading backup progress failed.", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        }

        private void Publish(CopyProgressMonitor monitor)
        {
            CopyProgress progress;
            try
            {
                progress = monitor.Poll(DateTime.Now);
            }
            catch (Exception ex)
            {
                AppLog.Error("Reading the progress of " + monitor.Key + " failed.", ex);
                return;
            }

            bool current;
            lock (_gate)
            {
                CopyProgressMonitor registered;
                current = _monitors.TryGetValue(monitor.Key, out registered) && ReferenceEquals(registered, monitor);
                if (current)
                {
                    _latest[monitor.Key] = progress;
                    if (monitor.IsComplete)
                    {
                        _finishedAt[monitor.Key] = DateTime.Now;

                        // A run that is over cannot be paused, so the flag must not linger and
                        // mark the next run as paused.
                        _paused.Remove(monitor.Key);
                    }
                }
            }

            // A monitor replaced while it was being polled is stale; its result is dropped.
            if (!current) return;

            EventHandler<ProgressEventArgs> handler = Changed;
            if (handler != null) handler(this, new ProgressEventArgs(progress));
        }

        // ------------------------------------------------------------------ finding running tasks

        private void Discover()
        {
            if (_probe == null) return;

            JobsFile settings = _settings();
            if (settings == null) return;

            List<JobConfig> candidates = new List<JobConfig>();
            lock (_gate)
            {
                foreach (JobConfig job in settings.Jobs)
                {
                    CopyProgressMonitor existing;
                    if (_monitors.TryGetValue(job.Key, out existing) && !existing.IsComplete) continue;

                    DateTime finished;
                    if (_finishedAt.TryGetValue(job.Key, out finished) &&
                        DateTime.Now - finished < RediscoverDelay) continue;

                    candidates.Add(job.Clone());
                }
            }

            foreach (JobConfig job in candidates)
            {
                TaskRunInfo info;
                try
                {
                    info = _probe(job);
                }
                catch (Exception ex)
                {
                    if (!_probeFailureLogged)
                    {
                        _probeFailureLogged = true;
                        AppLog.Error("Could not ask Task Scheduler whether " + job.TaskName + " is running.", ex);
                    }
                    continue;
                }

                if (info != null && info.Running)
                {
                    AppLog.Info("Attaching to the running task for " + job.Key + ".");
                    TrackRunning(job, info.StartedAt);
                }
            }
        }

        // ------------------------------------------------------------------ plumbing

        private TrackedRun NewRun(JobConfig job, string logsDir, DateTime startedAt, bool dryRun)
        {
            TrackedRun run = new TrackedRun();
            run.Job = job.Clone();
            run.LogsDir = logsDir;
            run.StartedAt = startedAt;
            run.IsDryRun = dryRun;

            string key = job.Key;
            run.IsPaused = new Func<bool>(delegate { return IsPaused(key); });

            if (_planner != null)
            {
                string source = job.SourcePath;
                string destination = job.DestinationPath;
                run.PlanFactory = new Func<CancellationToken, Task<PlanResult>>(
                    delegate(CancellationToken token) { return _planner.PlanAsync(source, destination, token); });
            }

            PlanEntry plan = TakePlan(job.Key);
            if (plan != null)
            {
                run.PrePlan = plan.Result;
                run.PrePlanSource = plan.Source;
            }
            return run;
        }

        private PlanEntry TakePlan(string key)
        {
            PlanEntry entry = null;
            lock (_gate)
            {
                if (_plans.TryGetValue(key, out entry)) _plans.Remove(key);
            }

            if (entry != null && DateTime.Now - entry.CreatedAt > PlanMaxAge)
            {
                Cancel(entry);
                return null;
            }
            return entry;
        }

        private static void Cancel(PlanEntry entry)
        {
            if (entry == null || entry.Source == null) return;
            try
            {
                entry.Source.Cancel();
            }
            catch (Exception)
            {
            }
        }

        private Func<bool> TaskProbe(JobConfig job)
        {
            if (_probe == null) return null;
            JobConfig copy = job.Clone();
            return new Func<bool>(delegate
            {
                TaskRunInfo info = _probe(copy);
                return info != null && info.Running;
            });
        }

        private void Attach(TrackedRun run)
        {
            CopyProgressMonitor monitor = new CopyProgressMonitor(run, _claims);
            CopyProgressMonitor old = null;

            lock (_gate)
            {
                if (_monitors.TryGetValue(run.Job.Key, out old)) _monitors.Remove(run.Job.Key);
                _monitors[run.Job.Key] = monitor;
                _latest.Remove(run.Job.Key);
                _finishedAt.Remove(run.Job.Key);
                _paused.Remove(run.Job.Key);
            }
            if (old != null) old.Dispose();

            // Show the new run straight away instead of after the next timer tick.
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate { RunPoll(); }));
        }

        private static long LengthOf(string path)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                return info.Exists ? info.Length : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public void Dispose()
        {
            List<CopyProgressMonitor> monitors;
            List<PlanEntry> plans;
            Timer timer;

            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                timer = _timer;
                _timer = null;
                monitors = new List<CopyProgressMonitor>(_monitors.Values);
                plans = new List<PlanEntry>(_plans.Values);
                _monitors.Clear();
                _plans.Clear();
            }

            if (timer != null) timer.Dispose();
            foreach (CopyProgressMonitor monitor in monitors) monitor.Dispose();
            foreach (PlanEntry plan in plans) Cancel(plan);
        }
    }
}
