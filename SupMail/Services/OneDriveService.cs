using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Desktop;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Kiota.Abstractions.Authentication;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SupMail.Services
{
    public class OneDriveService
    {
        private static readonly string[] Scopes = { "Files.ReadWrite" };

        private static IPublicClientApplication? _msalClient;
        private static bool _cacheInitialized;
        private GraphServiceClient? _graphClient;
        private AuthenticationResult? _authResult;

        public OneDriveService()
        {
        }

        private static async Task<IPublicClientApplication> GetOrCreateMsalClientAsync()
        {
            if (_msalClient != null && _cacheInitialized)
                return _msalClient;

            string clientId = AppConfig.OneDriveClientId;
            if (string.IsNullOrWhiteSpace(clientId))
                throw new InvalidOperationException("OneDrive Client ID is not configured. Please set it in appsettings.json.");

            _msalClient = PublicClientApplicationBuilder
                .Create(clientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, "common")
                .WithRedirectUri("http://localhost")
                .WithWindowsEmbeddedBrowserSupport()
                .Build();

            // Set up persistent token cache
            var cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SupMail");

            var storageProperties = new StorageCreationPropertiesBuilder("msal_cache.dat", cacheDir)
                .Build();

            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            cacheHelper.RegisterCache(_msalClient.UserTokenCache);
            _cacheInitialized = true;

            return _msalClient;
        }

        private async Task EnsureAuthenticatedAsync()
        {
            if (_authResult != null && _authResult.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                return; // Token still valid
            }

            var msalClient = await GetOrCreateMsalClientAsync();
            var accounts = await msalClient.GetAccountsAsync();
            var firstAccount = accounts.FirstOrDefault();

            try
            {
                // Try silent authentication first (uses cached token)
                _authResult = await msalClient
                    .AcquireTokenSilent(Scopes, firstAccount)
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                // Silent auth failed, use embedded WebView
                _authResult = await msalClient
                    .AcquireTokenInteractive(Scopes)
                    .WithUseEmbeddedWebView(true)
                    .ExecuteAsync();
            }

            _graphClient = new GraphServiceClient(new BaseBearerTokenAuthenticationProvider(
                new TokenProvider(_authResult.AccessToken)));
        }

        public async Task UploadFileAsync(string localFilePath, string onedriveFileName, string docNum)
        {
            await EnsureAuthenticatedAsync();

            using var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read);

            // Get the user's drive
            var drive = await _graphClient!.Me.Drive.GetAsync();

            // Upload to SupMail/{DocNum}/{fileName}
            string filePath = $"SupMail/{docNum}/{onedriveFileName}";
            await _graphClient.Drives[drive!.Id]
                .Items["root"]
                .ItemWithPath(filePath)
                .Content
                .PutAsync(fileStream);
        }
    }

    internal class TokenProvider : IAccessTokenProvider
    {
        private readonly string _accessToken;

        public TokenProvider(string accessToken)
        {
            _accessToken = accessToken;
        }

        public AllowedHostsValidator AllowedHostsValidator => new();

        public Task<string> GetAuthorizationTokenAsync(
            Uri uri,
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_accessToken);
        }
    }
}
