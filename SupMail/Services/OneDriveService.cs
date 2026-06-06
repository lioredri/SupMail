using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Drives.Item.Items.Item.CreateUploadSession;
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
        private static readonly string[] Scopes = { "Files.ReadWrite", "User.Read" };

        private static IPublicClientApplication? _msalClient;
        private static bool _cacheInitialized;
        private static GraphServiceClient? _graphClient;
        private static AuthenticationResult? _authResult;

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

        public async Task UploadFileAsync(string localFilePath, string onedriveFileName, string docNum, IProgress<long>? progress = null)
        {
            await EnsureAuthenticatedAsync();

            if (!File.Exists(localFilePath))
                throw new FileNotFoundException($"File not found: {localFilePath}");

            using var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var drive = await _graphClient!.Me.Drive.GetAsync()
                ?? throw new InvalidOperationException("Could not access OneDrive.");

            string filePath = $"SupMail/{docNum}/{onedriveFileName}";

            try
            {
                var uploadSessionRequestBody = new CreateUploadSessionPostRequestBody
                {
                    Item = new DriveItemUploadableProperties
                    {
                        AdditionalData = new Dictionary<string, object>
                        {
                            { "@microsoft.graph.conflictBehavior", "replace" }
                        }
                    }
                };

                var uploadSession = await _graphClient.Drives[drive.Id]
                    .Items["root"]
                    .ItemWithPath(filePath)
                    .CreateUploadSession
                    .PostAsync(uploadSessionRequestBody);

                if (uploadSession == null)
                    throw new InvalidOperationException("Failed to create OneDrive upload session.");

                int maxSliceSize = 320 * 1024;
                var uploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, fileStream, maxSliceSize, _graphClient.RequestAdapter);
                await uploadTask.UploadAsync(progress);
                return;
            }
            catch
            {
                // Fall back to direct upload if upload-session flow fails.
            }

            try
            {
                if (fileStream.CanSeek)
                    fileStream.Position = 0;

                using var progressStream = new ProgressReadStream(fileStream, progress);

                await _graphClient.Drives[drive.Id]
                    .Items["root"]
                    .ItemWithPath(filePath)
                    .Content
                    .PutAsync(progressStream);

                progress?.Report(fileStream.Length);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to upload '{onedriveFileName}': {ex.Message}", ex);
            }
        }

        public async Task<string> GetFolderLinkAsync(string docNum)
        {
            await EnsureAuthenticatedAsync();

            var drive = await _graphClient!.Me.Drive.GetAsync()
                ?? throw new InvalidOperationException("Could not access OneDrive.");

            string folderPath = $"SupMail/{docNum}";

            try
            {
                // Get the folder
                var folder = await _graphClient.Drives[drive.Id]
                    .Items["root"]
                    .ItemWithPath(folderPath)
                    .GetAsync();

                if (folder?.Id == null)
                    return string.Empty;

                // Create sharing link for the folder
                var linkRequest = new Microsoft.Graph.Drives.Item.Items.Item.CreateLink.CreateLinkPostRequestBody
                {
                    Type = "view",
                    Scope = "anonymous"
                };

                var permission = await _graphClient.Drives[drive.Id]
                    .Items[folder.Id]
                    .CreateLink
                    .PostAsync(linkRequest);

                return permission?.Link?.WebUrl ?? string.Empty;
            }
            catch (Exception ex)
            {
                // Folder link is optional, don't fail the whole operation
                System.Diagnostics.Debug.WriteLine($"[OneDrive] Failed to get folder link: {ex.Message}");
                return string.Empty;
            }
        }

        public async Task<string?> GetCurrentAccountNameAsync()
        {
            var msalClient = await GetOrCreateMsalClientAsync();
            var accounts = await msalClient.GetAccountsAsync();
            var account = accounts.FirstOrDefault();
            return account?.Username;
        }

        public async Task SignOutAsync()
        {
            var msalClient = await GetOrCreateMsalClientAsync();
            var accounts = await msalClient.GetAccountsAsync();

            foreach (var account in accounts)
            {
                await msalClient.RemoveAsync(account);
            }

            _authResult = null;
            _graphClient = null;
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

    internal sealed class ProgressReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly IProgress<long>? _progress;
        private long _totalRead;

        public ProgressReadStream(Stream inner, IProgress<long>? progress)
        {
            _inner = inner;
            _progress = progress;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _inner.Read(buffer, offset, count);
            Report(read);
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            int read = _inner.Read(buffer);
            Report(read);
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int read = await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
            Report(read);
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await _inner.ReadAsync(buffer, cancellationToken);
            Report(read);
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private void Report(int bytesRead)
        {
            if (bytesRead <= 0)
                return;

            _totalRead = Interlocked.Add(ref _totalRead, bytesRead);
            _progress?.Report(_totalRead);
        }
    }
}
