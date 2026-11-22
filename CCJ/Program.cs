using System;
using System.Threading;
using System.Windows.Forms;
using CCJ.Forms;
using CCJ.Services;

namespace CCJ
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Anything that escapes an event handler still reaches app.log instead of vanishing.
            Application.ThreadException += new ThreadExceptionEventHandler(OnThreadException);
            AppDomain.CurrentDomain.UnhandledException +=
                new UnhandledExceptionEventHandler(OnUnhandledException);

            if (args != null && args.Length > 0 &&
                string.Equals(args[0], "--selftest", StringComparison.OrdinalIgnoreCase))
            {
                Environment.ExitCode = SelfTest.Run();
                return;
            }

            AppLog.Info("CCJ started. Elevated=" + Elevation.IsElevated());
            Application.Run(new MainForm());
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            AppLog.Error("Unhandled exception on the UI thread.", e.Exception);
            MessageBox.Show(
                "Something went wrong:\r\n\r\n" + e.Exception.Message +
                "\r\n\r\nThe full details are in:\r\n" + Models.AppPaths.AppLogPath,
                "CCJ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            AppLog.Error("Unhandled exception outside the UI thread.", e.ExceptionObject as Exception);
        }
    }
}
