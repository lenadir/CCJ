using System;
using System.IO;
using System.Text;
using CCJ.Models;
using CCJ.Services;

namespace CCJ.Tests
{
    /// <summary>
    /// Builds robocopy log text and master.log lines in the exact shape the real tools write
    /// them, so the progress tests exercise the same parsing as a real run. Everything goes
    /// to folders under %TEMP%.
    /// </summary>
    public static class ProgressTestSupport
    {
        public static string NewTempDir(string prefix)
        {
            string dir = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static void DeleteQuietly(string dir)
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch (Exception)
            {
            }
        }

        public static JobConfig Job(string key, string nasFolder)
        {
            JobConfig job = new JobConfig();
            job.Key = key;
            job.JobName = key;
            job.SourcePath = @"\\192.168.100.205\vox\ENREGISTREMENT NV\JUILLET-AOUT";
            job.NasRoot = @"\\192.168.100.100\3G eng\ENREGISTREMENT NV";
            job.NasFolder = nasFolder;
            job.FirstRunDate = JobsFile.NextSunday();
            return job;
        }

        /// <summary>The robocopy log header, long enough that the Started line sits inside the first 600 bytes.</summary>
        public static string Header(string started)
        {
            StringBuilder b = new StringBuilder();
            b.Append("\r\n");
            b.Append("-------------------------------------------------------------------------------\r\n");
            b.Append("   ROBOCOPY     ::     Robust File Copy for Windows                              \r\n");
            b.Append("-------------------------------------------------------------------------------\r\n");
            b.Append("\r\n");
            b.Append("  Started : " + started + "\r\n");
            b.Append("   Source : C:\\Users\\Someone\\source\\\r\n");
            b.Append("     Dest : C:\\Users\\Someone\\destination\\\r\n");
            b.Append("\r\n");
            b.Append("    Files : *.*\r\n");
            b.Append("\t    \r\n");
            b.Append("  Options : *.* /S /E /DCOPY:T /COPY:DAT /Z /NP /XJ /R:10 /W:30 \r\n");
            b.Append("\r\n");
            b.Append("------------------------------------------------------------------------------\r\n");
            b.Append("\r\n");
            return b.ToString();
        }

        /// <summary>A file line the way robocopy writes it: class, size, name, separated by tabs.</summary>
        public static string FileLine(string name, long size)
        {
            return FileLine(name, size.ToString().PadLeft(8));
        }

        public static string FileLine(string name, string sizeText)
        {
            return "\t  New File  \t\t" + sizeText + "\t" + name + "\r\n";
        }

        public static string ExtraLine(string name, long size)
        {
            return "\t  *EXTRA File \t\t" + size.ToString().PadLeft(8) + "\t" + name + "\r\n";
        }

        public static string DirLine(string path, int count)
        {
            return "\t                " + count.ToString().PadLeft(4) + "\t" + path + "\r\n";
        }

        public static string ErrorLine(int code, string text)
        {
            return "2026/11/18 08:48:18 ERROR " + code + " (0x0000000" + code + ") " + text + "\r\n";
        }

        public static void Append(string path, string text)
        {
            using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                byte[] bytes = Encoding.ASCII.GetBytes(text);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        // ------------------------------------------------------------------ master.log lines

        public static string Started(string folder)
        {
            return "[18/11/2026 08:00:00,00] ===== " + folder + " job started: \"\\\\src\\vox\" to \"\\\\nas\\share\\" + folder + "\"\r\n";
        }

        public static string Attempt(int n, int max)
        {
            return "[18/11/2026 08:00:01,00] Attempt " + n + " of " + max + " \r\n";
        }

        public static string Problem(string text)
        {
            return "[18/11/2026 08:00:02,00] Problem: " + text + ". Waiting 900 seconds, then retrying.\r\n";
        }

        public static string Success(string folder, string logPath)
        {
            return "[18/11/2026 09:00:00,00] SUCCESS: " + folder + " copied, robocopy exit code 1. Log: " + logPath + "\r\n";
        }

        public static string Failed(string folder, string lastProblem)
        {
            return "[18/11/2026 09:00:00,00] FAILED: " + folder + " not copied after 6 attempts. Last problem: " + lastProblem + " \r\n";
        }

        public static string LogName(string folder, string stamp)
        {
            return folder + "_" + stamp + ".log";
        }
    }
}
