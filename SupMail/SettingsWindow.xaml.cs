using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace SupMail
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            txtApiUrl.Text = AppSettings.Current.ApiUrl;
            txtUser.Text = AppSettings.Current.Username;
            txtPass.Password = AppSettings.Current.Password;
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

            try
            {
                using HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{txtUser.Text}:{txtPass.Password}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

                var response = await client.GetAsync($"{apiUrl.TrimEnd('/')}/");
                if (response.IsSuccessStatusCode)
                    ShowTestStatus(success: true, "Connection successful");
                else
                    ShowTestStatus(success: false, $"Failed — {(int)response.StatusCode} {response.ReasonPhrase}");
            }
            catch (TaskCanceledException)
            {
                ShowTestStatus(success: false, "Connection timed out");
            }
            catch (Exception ex)
            {
                ShowTestStatus(success: false, ex.Message);
            }
            finally
            {
                btnTest.IsEnabled = true;
            }
        }

        private void ShowTestStatus(bool? success, string message)
        {
            pnlTestStatus.Visibility = Visibility.Visible;
            lblTestStatus.Text = message;

            if (success == true)
            {
                ellipseStatus.Fill = new SolidColorBrush(Color.FromRgb(16, 124, 16));  // green
                lblTestStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 16));
            }
            else if (success == false)
            {
                ellipseStatus.Fill = new SolidColorBrush(Color.FromRgb(196, 43, 28));  // red
                lblTestStatus.Foreground = new SolidColorBrush(Color.FromRgb(196, 43, 28));
            }
            else
            {
                ellipseStatus.Fill = new SolidColorBrush(Color.FromRgb(150, 150, 150)); // grey (in progress)
                lblTestStatus.Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100));
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var settings = new AppSettings
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