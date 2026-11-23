using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace CCJ.Models
{
    /// <summary>
    /// How the window should look. Kept in its own small file, ui.json, rather than in jobs.json:
    /// switching to dark mode must never depend on the backup settings being valid, and it must
    /// never be able to rewrite them.
    ///
    /// Nothing here can throw. A missing or damaged file simply means "light mode".
    /// </summary>
    public class UiSettings
    {
        public bool DarkMode { get; set; }

        public static UiSettings Load()
        {
            return Load(AppPaths.UiSettingsPath);
        }

        public static UiSettings Load(string path)
        {
            try
            {
                if (!File.Exists(path)) return new UiSettings();

                UiSettings loaded = JsonConvert.DeserializeObject<UiSettings>(
                    File.ReadAllText(path, Encoding.UTF8));

                return loaded ?? new UiSettings();
            }
            catch (Exception)
            {
                return new UiSettings();
            }
        }

        public bool Save()
        {
            return Save(AppPaths.UiSettingsPath);
        }

        public bool Save(string path)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented),
                    new UTF8Encoding(false));
                return true;
            }
            catch (Exception)
            {
                // A preference that cannot be remembered is not worth an error dialog.
                return false;
            }
        }
    }
}
