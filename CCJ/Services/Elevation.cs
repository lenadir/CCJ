using System;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;

namespace CCJ.Services
{
    /// <summary>
    /// The app runs unelevated so the dashboard, logs and checks always work. Writing a scheduled
    /// task that stores a password needs administrator rights, so the form offers a restart.
    /// </summary>
    public static class Elevation
    {
        public static bool IsElevated()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                if (identity == null) return false;
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not determine whether the process is elevated.", ex);
                return false;
            }
        }

        /// <summary>
        /// Starts a second copy of this exe with the runas verb. Returns true if the elevated
        /// copy started, in which case the caller should close this one.
        /// </summary>
        public static bool RestartElevated()
        {
            try
            {
                string exe = Assembly.GetEntryAssembly() != null
                    ? Assembly.GetEntryAssembly().Location
                    : Process.GetCurrentProcess().MainModule.FileName;

                ProcessStartInfo info = new ProcessStartInfo(exe);
                info.UseShellExecute = true;
                info.Verb = "runas";
                info.WorkingDirectory = System.IO.Path.GetDirectoryName(exe);
                Process.Start(info);
                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // 1223: the user cancelled the elevation prompt. Not an error worth logging loudly.
                return false;
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not restart elevated.", ex);
                return false;
            }
        }

        public static string CurrentUserName()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                return identity == null ? Environment.UserName : identity.Name;
            }
            catch (Exception)
            {
                return Environment.UserDomainName + "\\" + Environment.UserName;
            }
        }
    }
}
