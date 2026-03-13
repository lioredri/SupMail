using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Windows;

namespace SupMail
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            txtApiUrl.Text = Properties.Settings.Default.ApiUrl;
            txtUser.Text = Properties.Settings.Default.Username;
            txtPass.Password = Properties.Settings.Default.Password;
        }

        private async void btnTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string apiUrl = txtApiUrl.Text.Trim();
                if (string.IsNullOrWhiteSpace(apiUrl))
                {
                    MessageBox.Show("Please enter API Base URL.", "Priority API", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using (HttpClient client = new HttpClient())
                {
                    var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{txtUser.Text}:{txtPass.Password}"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

                    var response = await client.GetAsync($"{apiUrl.TrimEnd('/')}/$metadata");
                    if (response.IsSuccessStatusCode)
                        MessageBox.Show("Connection Successful!", "Priority API", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show($"Connection Failed: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ApiUrl = txtApiUrl.Text.Trim();
            Properties.Settings.Default.Username = txtUser.Text.Trim();
            Properties.Settings.Default.Password = txtPass.Password;

            Properties.Settings.Default.Save();
            this.DialogResult = true;
            this.Close();
        }
    }
}