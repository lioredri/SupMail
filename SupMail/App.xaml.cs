using System.IO;
using System.Windows;

namespace SupMail
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            System.AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                Exception ex = (Exception)args.ExceptionObject;
                string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
                string logPath = Path.Combine(desktopPath, "SupMail_Error.txt");

                string errorMessage = $"Time: {System.DateTime.Now}\n" +
                                     $"Error: {ex.Message}\n" +
                                     $"Source: {ex.Source}\n" +
                                     $"Stack Trace:\n{ex.StackTrace}\n" +
                                     (ex.InnerException != null ? $"Inner: {ex.InnerException.Message}" : "");

                File.WriteAllText(logPath, errorMessage);
                MessageBox.Show($"The app crashed. Check SupMail_Error.txt on your desktop.", "Fatal Error");
            };

            base.OnStartup(e);
        }
    }
}