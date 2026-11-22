using System;
using System.Globalization;
using System.IO;
using System.Text;
using CCJ.Models;

namespace CCJ.Services
{
    /// <summary>
    /// Appends to %APPDATA%\CCJ\app.log. The UI shows a short message; the full exception text
    /// always lands here.
    /// </summary>
    public static class AppLog
    {
        private static readonly object Gate = new object();
        private const long MaxBytes = 2 * 1024 * 1024;

        public static void Info(string message)
        {
            Write("INFO ", message);
        }

        public static void Warn(string message)
        {
            Write("WARN ", message);
        }

        public static void Error(string message, Exception ex)
        {
            string text = message;
            if (ex != null)
                text = message + Environment.NewLine + ex.ToString();
            Write("ERROR", text);
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (Gate)
                {
                    AppPaths.EnsureDataDir();
                    string path = AppPaths.AppLogPath;
                    RollIfTooBig(path);

                    string line = "[" +
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                        "] " + level + " " + message + Environment.NewLine;

                    File.AppendAllText(path, line, Encoding.UTF8);
                }
            }
            catch (Exception)
            {
                // Logging must never be the reason the app fails.
            }
        }

        private static void RollIfTooBig(string path)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists || info.Length < MaxBytes) return;

                string old = path + ".1";
                if (File.Exists(old)) File.Delete(old);
                File.Move(path, old);
            }
            catch (Exception)
            {
            }
        }
    }
}
