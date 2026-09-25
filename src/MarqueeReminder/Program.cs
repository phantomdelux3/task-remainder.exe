using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MarqueeReminder.Infrastructure;

namespace MarqueeReminder
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            using (var instance = SingleInstance.TryAcquire())
            {
                if (instance == null)
                {
                    SingleInstance.SignalRunningInstance();
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (sender, e) => ErrorReporter.Report(e.Exception, "An unexpected error occurred.");
                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                    ErrorReporter.Log(e.ExceptionObject as Exception ?? new Exception(Convert.ToString(e.ExceptionObject)), "Fatal error.");

                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

                var startMinimized = args.Contains(StartupRegistration.MinimizedArgument, StringComparer.OrdinalIgnoreCase);
                using (var context = new TrayApplicationContext(instance, startMinimized))
                {
                    Application.Run(context);
                }
            }
        }
    }
}
