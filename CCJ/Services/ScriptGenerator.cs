using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using CCJ.Models;

namespace CCJ.Services
{
    /// <summary>
    /// Renders Backup_FolderN.bat from the embedded template, which is a copy of the reference
    /// script Vox\Backup_Folder1.bat with placeholders in the SETTINGS block, the source line and
    /// the log folder line. Labels, the retry loop and the ERRORLEVEL checks are never touched.
    /// </summary>
    public class ScriptGenerator
    {
        public const string ResourceName = "CCJ.Resources.BackupTemplate.bat.txt";

        /// <summary>The robocopy options the reference script uses. Copy only; nothing is deleted.</summary>
        public const string RobocopyOptions = "/E /Z /XJ /R:10 /W:30 /COPY:DAT /DCOPY:T /NP";

        public const int MaxAttempts = 6;
        public const int WaitSeconds = 900;

        /// <summary>Options this app must never emit, because they delete files on the destination.</summary>
        private static readonly string[] ForbiddenOptions = new string[] { "/MIR", "/PURGE" };

        // Characters that would break the "set "NAME=value"" lines or inject a second command.
        private static readonly char[] UnsafeForBatch = new char[] { '"', '%', '\r', '\n' };

        private string _templateOverride;

        /// <summary>Test seam: lets a unit test supply the template instead of the resource.</summary>
        public ScriptGenerator()
        {
        }

        public ScriptGenerator(string templateOverride)
        {
            _templateOverride = templateOverride;
        }

        public string LoadTemplate()
        {
            if (_templateOverride != null)
                return _templateOverride;

            Assembly assembly = typeof(ScriptGenerator).Assembly;
            using (Stream stream = assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException(
                        "The embedded script template " + ResourceName + " is missing from " +
                        assembly.GetName().Name + ".dll/exe. The build is incomplete.");
                }
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>The real script for a job: copies files, creates the destination folder.</summary>
        public string Render(JobConfig job, string voxRoot)
        {
            return RenderInternal(job, Path.Combine(voxRoot, "Logs"), false);
        }

        /// <summary>
        /// The dry run script: robocopy gets /L so it only lists, the destination folder is not
        /// created, and the logs go to a folder under %TEMP%. It can never copy a file.
        /// </summary>
        public string RenderDryRun(JobConfig job, string dryRunLogDir)
        {
            return RenderInternal(job, dryRunLogDir, true);
        }

        private string RenderInternal(JobConfig job, string logDir, bool dryRun)
        {
            if (job == null) throw new ArgumentNullException("job");
            if (string.IsNullOrEmpty(logDir)) throw new ArgumentException("logDir is required.", "logDir");

            string source = job.SourcePath == null ? "" : job.SourcePath.TrimEnd('\\');
            string nasRoot = job.NasRoot == null ? "" : job.NasRoot.TrimEnd('\\');
            string folder = job.NasFolder == null ? "" : job.NasFolder.Trim();

            CheckBatchSafe("source path", source);
            CheckBatchSafe("NAS root", nasRoot);
            CheckBatchSafe("NAS folder name", folder);
            CheckBatchSafe("log folder", logDir);
            CheckBatchSafe("job key", job.Key);

            string text = LoadTemplate();

            text = text.Replace("{{FILE_SUFFIX}}", job.Key);
            text = text.Replace("{{DST_DESCRIPTION}}", nasRoot + "\\" + folder);
            text = text.Replace("{{SRC_SERVER}}", HostOf(source));
            text = text.Replace("{{SRC_SHARE}}", ShareOf(source));
            text = text.Replace("{{DST_ROOT}}", nasRoot);
            text = text.Replace("{{FOLDER}}", folder);
            text = text.Replace("{{MAX_ATTEMPTS}}", MaxAttempts.ToString(CultureInfo.InvariantCulture));
            text = text.Replace("{{WAIT_SECONDS}}", WaitSeconds.ToString(CultureInfo.InvariantCulture));
            text = text.Replace("{{SRC}}", source);
            text = text.Replace("{{LOGDIR}}", logDir.TrimEnd('\\'));

            if (dryRun)
            {
                text = text.Replace("{{MKDIR_DST}}",
                    "rem  DRY RUN: the destination folder is deliberately not created.");
                text = text.Replace("{{DRY_RUN_FLAG}}", " /L");
            }
            else
            {
                // Byte for byte the line from the reference script.
                text = text.Replace("{{MKDIR_DST}}", "if not exist \"%DST%\\\" mkdir \"%DST%\"");
                text = text.Replace("{{DRY_RUN_FLAG}}", "");
            }

            text = NormalizeToCrLf(text);

            GuardAgainstDeletingOptions(text);
            GuardTemplateFullyRendered(text);
            return text;
        }

        /// <summary>Batch files must be CRLF, whatever line endings the template file has on disk.</summary>
        public static string NormalizeToCrLf(string text)
        {
            if (text == null) return null;
            text = text.Replace("\r\n", "\n");
            text = text.Replace("\r", "\n");
            return text.Replace("\n", "\r\n");
        }

        /// <summary>
        /// Hard stop if a rendered script would ever delete files on the destination. Comment
        /// lines are skipped, because the reference script's own header says "no /MIR, no /PURGE".
        /// </summary>
        public static void GuardAgainstDeletingOptions(string script)
        {
            if (script == null) return;

            string[] lines = script.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("rem", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.StartsWith("::")) continue;

                string upper = line.ToUpperInvariant();
                foreach (string forbidden in ForbiddenOptions)
                {
                    if (upper.Contains(forbidden))
                    {
                        throw new InvalidOperationException(
                            "Refusing to write this script: line " + (i + 1) + " contains " +
                            forbidden + ", which deletes files on the destination. CCJ never " +
                            "generates a script that deletes anything.");
                    }
                }
            }
        }

        private static void GuardTemplateFullyRendered(string script)
        {
            int open = script.IndexOf("{{", StringComparison.Ordinal);
            if (open >= 0)
            {
                int close = script.IndexOf("}}", open, StringComparison.Ordinal);
                string token = close > open
                    ? script.Substring(open, close - open + 2)
                    : script.Substring(open, Math.Min(24, script.Length - open));
                throw new InvalidOperationException(
                    "The script template still contains the unreplaced placeholder " + token + ".");
            }
        }

        private static void CheckBatchSafe(string what, string value)
        {
            if (value == null) return;
            int bad = value.IndexOfAny(UnsafeForBatch);
            if (bad >= 0)
            {
                char c = value[bad];
                string shown = c == '\r' ? "a carriage return" : c == '\n' ? "a line break" : "'" + c + "'";
                throw new InvalidOperationException(
                    "The " + what + " contains " + shown + ", which cannot be written safely into a " +
                    "batch file. Value: " + value);
            }
        }

        /// <summary>192.168.100.205 from \\192.168.100.205\vox\something</summary>
        public static string HostOf(string uncPath)
        {
            string[] parts = SplitUnc(uncPath);
            return parts.Length > 0 ? parts[0] : "";
        }

        /// <summary>vox from \\192.168.100.205\vox\something</summary>
        public static string ShareOf(string uncPath)
        {
            string[] parts = SplitUnc(uncPath);
            return parts.Length > 1 ? parts[1] : "";
        }

        private static string[] SplitUnc(string uncPath)
        {
            if (string.IsNullOrEmpty(uncPath)) return new string[0];

            // Only a real UNC path has a host and a share. A local path such as C:\Vox has
            // neither, so nothing is reported rather than "C:" being passed off as a server.
            if (!uncPath.StartsWith(@"\\", StringComparison.Ordinal)) return new string[0];

            string trimmed = uncPath.TrimStart('\\');
            return trimmed.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Writes the script. Batch files are read by cmd.exe in the console (OEM) code page, so
        /// that is what we write, and never with a byte order mark, which would break "@echo off".
        /// </summary>
        public void WriteScript(string path, string content)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is required.", "path");
            GuardAgainstDeletingOptions(content);

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(path, BatchEncoding.GetBytes(NormalizeToCrLf(content)));
        }

        /// <summary>The console code page, with a byte order mark never emitted.</summary>
        public static Encoding BatchEncoding
        {
            get
            {
                try
                {
                    return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
                }
                catch (Exception)
                {
                    return new UTF8Encoding(false);
                }
            }
        }
    }
}
