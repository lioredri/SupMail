using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using SupMail.Helpers;

namespace SupMail.Services
{
    public class PriorityApiService
    {
        private readonly HttpClient _client;

        public PriorityApiService()
        {
            _client = new HttpClient();
            ConfigureAuthentication();
        }

        public PriorityApiService(HttpClient client)
        {
            _client = client;
            ConfigureAuthentication();
        }

        private void ConfigureAuthentication()
        {
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes(
                $"{SettingsService.Current.Username}:{SettingsService.Current.Password}"));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        }

        public async Task<string> GetPurchaseOrderAsync(string docNum)
        {
            string url = $"{SettingsService.Current.ApiUrl.TrimEnd('/')}/PORDERS('{docNum}')?$select=ORDNAME,AMAIL&$expand=ITML_SUPMAIL_SUBFORM";

            System.Diagnostics.Debug.WriteLine($"[REQUEST URL]: {url}");

            var response = await _client.GetAsync(url);
            string responseBody = await response.Content.ReadAsStringAsync();

            System.Diagnostics.Debug.WriteLine($"[RESPONSE STATUS]: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"[RESPONSE BODY]: {responseBody}");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Request failed with status {response.StatusCode}");
            }

            return responseBody;
        }

        public async Task<string> SaveDataUriToTempFileAsync(string dataUri, string? fileDescription)
        {
            string? base64 = FileHelpers.NormalizeBase64(dataUri);
            if (string.IsNullOrWhiteSpace(base64))
                throw new Exception("Data URI did not contain valid base64 content.");

            byte[] bytes = Convert.FromBase64String(base64);

            string tempFolder = Path.Combine(Path.GetTempPath(), "SupMail", "Attachments");
            Directory.CreateDirectory(tempFolder);
            string fileName = !string.IsNullOrWhiteSpace(fileDescription)
               ? fileDescription
               : $"attachment-{Guid.NewGuid():N}.bin";
            string tempFile = Path.Combine(tempFolder, fileName);
            await File.WriteAllBytesAsync(tempFile, bytes);

            return tempFile;
        }

        public async Task<string> DownloadSystemAttachmentAsync(string systemPath, string? intendedFileName)
        {
            string normalizedPath = systemPath.Replace("\\", "/");
            string originalFileName = normalizedPath.Split('/').LastOrDefault() ?? string.Empty;

            string relativePath = normalizedPath
                .TrimStart('.')
                .TrimStart('/');

            string requestUrl = $"{SettingsService.Current.ApiUrl.TrimEnd('/')}/{relativePath}";
            var response = await _client.GetAsync(requestUrl);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to download system attachment ({response.StatusCode}).");

            string? base64 = FileHelpers.ExtractBase64(responseBody);
            if (string.IsNullOrWhiteSpace(base64))
                throw new Exception("System attachment response did not contain valid base64 content.");

            byte[] bytes = Convert.FromBase64String(base64);

            string fileName = !string.IsNullOrWhiteSpace(intendedFileName)
                ? intendedFileName
                : (!string.IsNullOrWhiteSpace(originalFileName)
                    ? originalFileName
                    : $"attachment-{Guid.NewGuid():N}.bin");

            string tempFolder = Path.Combine(Path.GetTempPath(), "SupMail", "Attachments");
            Directory.CreateDirectory(tempFolder);

            string tempFile = Path.Combine(tempFolder, fileName);
            await File.WriteAllBytesAsync(tempFile, bytes);

            return tempFile;
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
