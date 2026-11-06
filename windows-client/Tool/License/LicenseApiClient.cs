using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Tool.Updates;

namespace Tool.License
{
    public interface ILicenseApiClient
    {
        Task<ApiResponse<ActivateResponseData>> ActivateAsync(
            string licenseKey,
            string deviceId,
            string deviceName,
            string osName,
            string osVersion,
            string appVersion,
            CancellationToken ct = default);

        Task<ApiResponse<ValidateResponseData>> ValidateAsync(
            string deviceToken,
            string deviceId,
            string appVersion,
            CancellationToken ct = default);

        Task<ApiResponse<HeartbeatResponseData>> HeartbeatAsync(
            string deviceToken,
            string appVersion,
            CancellationToken ct = default);

        Task<ApiResponse<object>> DeactivateAsync(
            string deviceToken,
            string reason,
            CancellationToken ct = default);

        Task<ApiResponse<VersionResponseData>> GetVersionAsync(
            string productCode,
            string currentVersion,
            CancellationToken ct = default);
    }

    public class LicenseApiClient : ILicenseApiClient
    {
        public const string DefaultProductionUrl = "https://license-api.veasnag8.workers.dev";

        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public LicenseApiClient(string? baseUrl = null, HttpClient? customClient = null)
        {
            var envUrl = Environment.GetEnvironmentVariable("LICENSE_API_URL");
            _baseUrl = (!string.IsNullOrWhiteSpace(baseUrl)
                ? baseUrl
                : (!string.IsNullOrWhiteSpace(envUrl) ? envUrl : DefaultProductionUrl)).TrimEnd('/');

            if (customClient != null)
            {
                _httpClient = customClient;
            }
            else
            {
                var handler = new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    SslOptions = new SslClientAuthenticationOptions
                    {
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                    }
                };
                _httpClient = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(15)
                };
            }
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WindowsClient-EXE/1.0");
        }

        public async Task<ApiResponse<ActivateResponseData>> ActivateAsync(
            string licenseKey,
            string deviceId,
            string deviceName,
            string osName,
            string osVersion,
            string appVersion,
            CancellationToken ct = default)
        {
            var req = new ActivateRequest
            {
                LicenseKey = licenseKey,
                DeviceId = deviceId,
                DeviceName = deviceName,
                OsName = osName,
                OsVersion = osVersion,
                AppVersion = appVersion
            };

            return await PostJsonAsync<ActivateRequest, ActivateResponseData>("/v1/activate", req, null, ct);
        }

        public async Task<ApiResponse<ValidateResponseData>> ValidateAsync(
            string deviceToken,
            string deviceId,
            string appVersion,
            CancellationToken ct = default)
        {
            var req = new ValidateRequest
            {
                DeviceId = deviceId,
                AppVersion = appVersion
            };

            return await PostJsonAsync<ValidateRequest, ValidateResponseData>("/v1/validate", req, deviceToken, ct);
        }

        public async Task<ApiResponse<HeartbeatResponseData>> HeartbeatAsync(
            string deviceToken,
            string appVersion,
            CancellationToken ct = default)
        {
            var req = new HeartbeatRequest
            {
                AppVersion = appVersion
            };

            return await PostJsonAsync<HeartbeatRequest, HeartbeatResponseData>("/v1/heartbeat", req, deviceToken, ct);
        }

        public async Task<ApiResponse<object>> DeactivateAsync(
            string deviceToken,
            string reason,
            CancellationToken ct = default)
        {
            var req = new DeactivateRequest { Reason = reason };
            return await PostJsonAsync<DeactivateRequest, object>("/v1/deactivate", req, deviceToken, ct);
        }

        public async Task<ApiResponse<VersionResponseData>> GetVersionAsync(
            string productCode,
            string currentVersion,
            CancellationToken ct = default)
        {
            try
            {
                var url = $"{_baseUrl}/v1/version?product={Uri.EscapeDataString(productCode)}&current_version={Uri.EscapeDataString(currentVersion)}";
                using var response = await _httpClient.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    var parsed = JsonSerializer.Deserialize<ApiResponse<VersionResponseData>>(json);
                    if (parsed != null && parsed.Success && parsed.Data != null)
                    {
                        return parsed;
                    }
                }
            }
            catch { }

            // Automatic fallback to GitHub raw version.json if Cloudflare worker fails or returns 500
            try
            {
                using var ghClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                ghClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                string ghUrl = $"https://raw.githubusercontent.com/veasnag8/Management-Tools/main/version.json?t={DateTime.UtcNow.Ticks}";
                using var ghResp = await ghClient.GetAsync(ghUrl, ct);
                if (ghResp.IsSuccessStatusCode)
                {
                    var ghJson = await ghResp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(ghJson);
                    var root = doc.RootElement;
                    var data = new VersionResponseData
                    {
                        CurrentVersion = currentVersion,
                        LatestVersion = root.TryGetProperty("latest_version", out var lv) ? (lv.GetString() ?? currentVersion) : currentVersion,
                        DownloadUrl = root.TryGetProperty("download_url", out var du) ? (du.GetString() ?? "") : "",
                        ReleaseNotes = root.TryGetProperty("release_notes", out var rn) ? (rn.GetString() ?? "") : "",
                        Sha256 = root.TryGetProperty("sha256", out var sha) ? (sha.GetString() ?? "") : "",
                        Mandatory = root.TryGetProperty("mandatory", out var man) && man.GetBoolean(),
                        HasUpdate = root.TryGetProperty("has_update", out var hu) && hu.GetBoolean()
                    };
                    if (Version.TryParse(data.LatestVersion, out var remoteVer) && Version.TryParse(currentVersion, out var localVer))
                    {
                        data.HasUpdate = remoteVer > localVer;
                    }
                    return new ApiResponse<VersionResponseData> { Success = true, Data = data };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<VersionResponseData>
                {
                    Success = false,
                    Error = new ApiError { Code = "NETWORK_ERROR", Message = ex.Message }
                };
            }

            return new ApiResponse<VersionResponseData>
            {
                Success = false,
                Error = new ApiError { Code = "VERSION_CHECK_FAILED", Message = "Unable to retrieve version info." }
            };
        }

        private async Task<ApiResponse<TRes>> PostJsonAsync<TReq, TRes>(
            string path,
            TReq requestBody,
            string? bearerToken,
            CancellationToken ct)
        {
            try
            {
                var url = $"{_baseUrl}{path}";
                using var request = new HttpRequestMessage(HttpMethod.Post, url);

                if (!string.IsNullOrEmpty(bearerToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                }

                var jsonPayload = JsonSerializer.Serialize(requestBody);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    var result = JsonSerializer.Deserialize<ApiResponse<TRes>>(content);
                    if (result != null) return result;
                }

                return new ApiResponse<TRes>
                {
                    Success = false,
                    Error = new ApiError
                    {
                        Code = $"HTTP_{(int)response.StatusCode}",
                        Message = $"Server returned HTTP status {(int)response.StatusCode}"
                    }
                };
            }
            catch (TaskCanceledException)
            {
                return new ApiResponse<TRes>
                {
                    Success = false,
                    Error = new ApiError { Code = "TIMEOUT", Message = "Request timed out." }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<TRes>
                {
                    Success = false,
                    Error = new ApiError { Code = "NETWORK_ERROR", Message = ex.Message }
                };
            }
        }
    }

    public class ValidateRequest
    {
        [JsonPropertyName("device_id")]
        public string? DeviceId { get; set; }

        [JsonPropertyName("app_version")]
        public string? AppVersion { get; set; }
    }

    public class HeartbeatRequest
    {
        [JsonPropertyName("app_version")]
        public string? AppVersion { get; set; }
    }

    public class DeactivateRequest
    {
        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }
}
