using System;
using System.Net.Http;
using System.Net.Http.Headers;
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
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public LicenseApiClient(string baseUrl = "http://localhost:8787", HttpClient? customClient = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = customClient ?? new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
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
                var json = await response.Content.ReadAsStringAsync(ct);
                return ParseResponse<VersionResponseData>(json, (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return new ApiResponse<VersionResponseData>
                {
                    Success = false,
                    Error = new ApiError { Code = "NETWORK_ERROR", Message = ex.Message }
                };
            }
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

                return ParseResponse<TRes>(content, (int)response.StatusCode);
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

        private static ApiResponse<TRes> ParseResponse<TRes>(string content, int httpStatusCode)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new ApiResponse<TRes>
                {
                    Success = httpStatusCode >= 200 && httpStatusCode < 300,
                    Error = httpStatusCode >= 200 && httpStatusCode < 300 ? null : new ApiError { Code = $"HTTP_{httpStatusCode}", Message = $"Server returned HTTP status {httpStatusCode}" }
                };
            }

            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                bool success = false;
                if (root.TryGetProperty("success", out var successProp))
                {
                    success = successProp.ValueKind == JsonValueKind.True;
                }
                else if (httpStatusCode >= 200 && httpStatusCode < 300)
                {
                    success = true;
                }

                TRes? data = default;
                if (root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind != JsonValueKind.Null && dataProp.ValueKind != JsonValueKind.Undefined)
                {
                    data = JsonSerializer.Deserialize<TRes>(dataProp.GetRawText());
                }

                ApiError? error = null;
                if (root.TryGetProperty("error", out var errorProp))
                {
                    if (errorProp.ValueKind == JsonValueKind.Object)
                    {
                        var code = errorProp.TryGetProperty("code", out var c) ? c.GetString() ?? "UNKNOWN" : "UNKNOWN";
                        var msg = errorProp.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
                        error = new ApiError { Code = code, Message = msg };
                    }
                    else if (errorProp.ValueKind == JsonValueKind.String)
                    {
                        error = new ApiError { Code = $"HTTP_{httpStatusCode}", Message = errorProp.GetString() ?? "Server error" };
                    }
                }
                else if (root.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String)
                {
                    error = new ApiError { Code = $"HTTP_{httpStatusCode}", Message = msgProp.GetString() ?? "Server error" };
                }

                if (!success && error == null)
                {
                    error = new ApiError { Code = $"HTTP_{httpStatusCode}", Message = $"Server returned HTTP status {httpStatusCode}" };
                }

                return new ApiResponse<TRes>
                {
                    Success = success,
                    Data = data,
                    Error = error
                };
            }
            catch
            {
                return new ApiResponse<TRes>
                {
                    Success = false,
                    Error = new ApiError { Code = $"HTTP_{httpStatusCode}", Message = content }
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
