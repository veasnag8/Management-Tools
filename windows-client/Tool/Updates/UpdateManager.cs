using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Tool.Updates
{
    public class VersionResponseData
    {
        [JsonPropertyName("product_code")]
        public string ProductCode { get; set; } = string.Empty;

        [JsonPropertyName("current_version")]
        public string CurrentVersion { get; set; } = string.Empty;

        [JsonPropertyName("latest_version")]
        public string LatestVersion { get; set; } = string.Empty;

        [JsonPropertyName("download_url")]
        public string? DownloadUrl { get; set; }

        [JsonPropertyName("release_notes")]
        public string? ReleaseNotes { get; set; }

        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        [JsonPropertyName("mandatory")]
        public bool Mandatory { get; set; }

        [JsonPropertyName("has_update")]
        public bool HasUpdate { get; set; }
    }

    public interface IUpdateManager
    {
        Task<VersionResponseData?> CheckForUpdateAsync(string productCode, string currentVersion, CancellationToken ct = default);
        Task<string?> DownloadAndVerifyInstallerAsync(VersionResponseData updateInfo, IProgress<int>? progress = null, CancellationToken ct = default);
        void LaunchInstaller(string installerPath);
    }

    public class UpdateManager : IUpdateManager
    {
        private readonly HttpClient _httpClient;
        private readonly License.ILicenseApiClient _apiClient;

        public UpdateManager(License.ILicenseApiClient apiClient, HttpClient? httpClient = null)
        {
            _apiClient = apiClient;
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        public async Task<VersionResponseData?> CheckForUpdateAsync(
            string productCode,
            string currentVersion,
            CancellationToken ct = default)
        {
            var res = await _apiClient.GetVersionAsync(productCode, currentVersion, ct);
            if (res.Success && res.Data != null && res.Data.HasUpdate)
            {
                return res.Data;
            }
            return null;
        }

        public async Task<string?> DownloadAndVerifyInstallerAsync(
            VersionResponseData updateInfo,
            IProgress<int>? progress = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(updateInfo.DownloadUrl))
            {
                return null;
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"ToolUpdate_{updateInfo.LatestVersion}.exe");

            // 1. Download file with progress
            using (var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                using (var contentStream = await response.Content.ReadAsStreamAsync(ct))
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    long totalRead = 0;
                    int read;

                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read, ct);
                        totalRead += read;
                        if (totalBytes > 0 && progress != null)
                        {
                            var pct = (int)((totalRead * 100) / totalBytes);
                            progress.Report(pct);
                        }
                    }
                }
            }

            // 2. Verify SHA-256 Checksum if provided
            if (!string.IsNullOrWhiteSpace(updateInfo.Sha256))
            {
                using var sha256 = SHA256.Create();
                using var fileStream = File.OpenRead(tempPath);
                var hashBytes = await sha256.ComputeHashAsync(fileStream, ct);
                var computedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                var expectedHash = updateInfo.Sha256.Trim().ToLowerInvariant();

                if (!string.Equals(computedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    // Checksum mismatch! Delete untrusted file
                    try { File.Delete(tempPath); } catch { }
                    throw new InvalidOperationException($"Installer integrity verification failed. Expected SHA256 {expectedHash}, got {computedHash}");
                }
            }

            return tempPath;
        }

        public void LaunchInstaller(string installerPath)
        {
            if (File.Exists(installerPath))
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
        }
    }
}
