using System;
using System.Threading;
using System.Windows.Forms;

namespace TPMSimpleModMaker
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                ShowFatalError(ex, "Application.Run");
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            ShowFatalError(e == null ? null : e.Exception, "Windows Forms UI thread");
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e == null ? null : e.ExceptionObject as Exception;
            try { BetaSupportService.WriteCrashReport(ex, "Unhandled AppDomain exception"); }
            catch { }
        }

        private static void ShowFatalError(Exception ex, string origin)
        {
            string crashPath = null;
            try { crashPath = BetaSupportService.WriteCrashReport(ex, origin); }
            catch { }

            string message = "Memento Maker encountered an unexpected error.";
            if (!string.IsNullOrEmpty(crashPath))
                message += "\n\nA crash report was saved to:\n" + crashPath;
            message += "\n\nPlease include a Support Bundle if you need help with this issue.";

            try
            {
                TwoPointTheme.ShowMessage(message, "Memento Maker - Unexpected Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                MessageBox.Show(message, "Memento Maker - Unexpected Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
