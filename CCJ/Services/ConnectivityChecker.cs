using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using CCJ.Models;

namespace CCJ.Services
{
    /// <summary>
    /// Reachability checks for the source and the NAS. Everything runs off the UI thread, because
    /// Directory.Exists on a dead UNC path blocks for many seconds.
    /// </summary>
    public class ConnectivityChecker
    {
        public const int SmbPort = 445;
        public const int DefaultTimeoutMs = 3000;

        /// <summary>
        /// Checks every distinct host and path used by the enabled jobs: the folder exists, and
        /// TCP 445 answers within the timeout.
        /// </summary>
        public async Task<List<CheckResult>> CheckJobsAsync(JobsFile file, int timeoutMs)
        {
            List<CheckResult> results = new List<CheckResult>();
            if (file == null) return results;

            List<string> hostsSeen = new List<string>();
            List<string> pathsSeen = new List<string>();

            foreach (JobConfig job in file.Jobs)
            {
                string[] paths = new string[] { job.SourcePath, job.NasRoot };
                string[] labels = new string[] { job.Key + " source", job.Key + " NAS root" };

                for (int i = 0; i < paths.Length; i++)
                {
                    string path = paths[i];
                    if (string.IsNullOrEmpty(path)) continue;

                    string host = ScriptGenerator.HostOf(path);
                    if (host.Length > 0 && !ContainsIgnoreCase(hostsSeen, host))
                    {
                        hostsSeen.Add(host);
                        results.Add(await CheckPortAsync(host, SmbPort, timeoutMs));
                    }

                    if (!ContainsIgnoreCase(pathsSeen, path))
                    {
                        pathsSeen.Add(path);
                        results.Add(await CheckPathAsync(labels[i], path, timeoutMs));
                    }
                }
            }

            if (results.Count == 0)
                results.Add(new CheckResult("Nothing to check", false, "No source or NAS path is set."));

            return results;
        }

        /// <summary>Does the folder exist and can we list it, within the timeout?</summary>
        public async Task<CheckResult> CheckPathAsync(string label, string path, int timeoutMs)
        {
            if (timeoutMs <= 0) timeoutMs = DefaultTimeoutMs;

            Task<string> probe = Task.Run(new Func<string>(delegate
            {
                try
                {
                    if (!Directory.Exists(path))
                        return "not reachable (does not exist, or access denied)";
                    return null;
                }
                catch (Exception ex)
                {
                    return ex.GetType().Name + ": " + ex.Message;
                }
            }));

            Task finished = await Task.WhenAny(probe, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (finished != probe)
            {
                return new CheckResult(label, false,
                    path + " did not answer within " + timeoutMs + " ms.");
            }

            string problem = probe.Result;
            if (problem == null)
                return new CheckResult(label, true, path + " is reachable.");

            return new CheckResult(label, false, path + " " + problem);
        }

        /// <summary>Does the host answer on the SMB port within the timeout?</summary>
        public async Task<CheckResult> CheckPortAsync(string host, int port, int timeoutMs)
        {
            if (timeoutMs <= 0) timeoutMs = DefaultTimeoutMs;
            string label = "TCP " + port + " on " + host;

            TcpClient client = new TcpClient();
            try
            {
                Task connect = client.ConnectAsync(host, port);
                Task finished = await Task.WhenAny(connect, Task.Delay(timeoutMs)).ConfigureAwait(false);

                if (finished != connect)
                {
                    return new CheckResult(label, false,
                        "no answer within " + timeoutMs + " ms (file sharing blocked, or host down).");
                }

                await connect.ConfigureAwait(false);
                return new CheckResult(label, true, "answered.");
            }
            catch (Exception ex)
            {
                return new CheckResult(label, false, ex.Message);
            }
            finally
            {
                try
                {
                    client.Close();
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// Counts the files under a folder, for the confirmation dialog. Returns null if the count
        /// cannot be taken, so the dialog can say "unknown" rather than guess.
        /// </summary>
        public async Task<long?> CountFilesAsync(string path, int timeoutMs)
        {
            if (timeoutMs <= 0) timeoutMs = 15000;

            Task<long?> count = Task.Run(new Func<long?>(delegate
            {
                try
                {
                    if (!Directory.Exists(path)) return null;
                    long total = 0;
                    foreach (string unused in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        total++;
                        if (total > 5000000) break;
                    }
                    return total;
                }
                catch (Exception ex)
                {
                    AppLog.Warn("Could not count files under " + path + ": " + ex.Message);
                    return null;
                }
            }));

            Task finished = await Task.WhenAny(count, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (finished != count) return null;
            return count.Result;
        }

        private static bool ContainsIgnoreCase(List<string> list, string value)
        {
            foreach (string item in list)
            {
                if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
