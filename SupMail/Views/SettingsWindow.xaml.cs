using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Threading;
using System.IO;
using SupMail.Services;

namespace SupMail.Views
{
    public partial class SettingsWindow : Window
    {
        private static readonly HttpClient SharedClient = new HttpClient();
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SupMail", "connection_test.log");

        private void LogMessage(string message)
        {
            try
            {
                var logDir = Path.GetDirectoryName(LogFilePath);
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir!);

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                File.AppendAllText(LogFilePath, $"[{timestamp}] {message}\n");
            }
            catch { }
        }

        public SettingsWindow()
        {
            InitializeComponent();
            txtApiUrl.Text = SettingsService.Current.ApiUrl;
            txtUser.Text = SettingsService.Current.Username;
            txtPass.Password = SettingsService.Current.Password;
        }

        private async void btnTest_Click(object sender, RoutedEventArgs e)
        {
            string apiUrl = txtApiUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                ShowTestStatus(success: false, "Please enter an API Base URL first.");
                return;
            }

            btnTest.IsEnabled = false;
            ShowTestStatus(success: null, "Testing...");

            LogMessage("=== Connection Test Started ===");
            LogMessage($"API URL: {apiUrl}");
            LogMessage($"Username: {txtUser.Text}");

            try
            {
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{txtUser.Text}:{txtPass.Password}"));
                var requestUrl = $"{apiUrl.TrimEnd('/')}/GetPriorityVersion()";
                LogMessage($"Request URL: {requestUrl}");

                var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
                LogMessage("Authorization header set");

                LogMessage("Creating CancellationTokenSource with 10 second timeout");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                LogMessage("Sending request...");
                var startTime = DateTime.Now;
                var response = await SharedClient.SendAsync(request, cts.Token);
                var duration = DateTime.Now - startTime;

                LogMessage($"Response received in {duration.TotalMilliseconds:F2}ms");
                LogMessage($"Status Code: {(int)response.StatusCode} {response.ReasonPhrase}");

                if (response.IsSuccessStatusCode)
                {
                    LogMessage("Connection successful");
                    ShowTestStatus(success: true, "Connection successful");
                }
                else
                {
                    var errorMsg = $"Failed \u2014 {(int)response.StatusCode} {response.ReasonPhrase}";
                    LogMessage($"Connection failed: {errorMsg}");
                    ShowTestStatus(success: false, errorMsg);
                }
            }
            catch (OperationCanceledException ex)
            {
                LogMessage($"OperationCanceledException: {ex.Message}");
                ShowTestStatus(success: false, "Connection timed out");
            }
            catch (Exception ex)
            {
                LogMessage($"Exception: {ex.GetType().Name}: {ex.Message}");
                LogMessage($"Stack trace: {ex.StackTrace}");
                ShowTestStatus(success: false, ex.Message);
            }
            finally
            {
                LogMessage("=== Connection Test Ended ===\n");
                btnTest.IsEnabled = true;
            }
        }

        private void ShowTestStatus(bool? success, string message)
        {
            pnlTestStatus.Visibility = Visibility.Visible;
            lblTestStatus.Text = message;

            if (success == true)
            {
                ellipseStatus.Fill = new SolidColorBrush(Color.FromRgb(16, 124, 16));
                lblTestStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 16));
            }
            else if (success == false)
            {
                ellipseStatus.Fill = new SolidColorBrush(Color.FromRgb(196, 43, 28));
                lblTestStatus.Foreground = new SolidColorBrush(Color.FromRgb(196, 43, 28));
            }
            else
            {
                ellipseStatus.Fill = new SolidColorBrush(Color.FromRgb(150, 150, 150));
                lblTestStatus.Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100));
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var settings = new SettingsService
            {
                ApiUrl = txtApiUrl.Text.Trim(),
                Username = txtUser.Text.Trim(),
                Password = txtPass.Password
            };
            settings.Save();

            this.DialogResult = true;
            this.Close();
        }
    }
}
