using System;
using System.Text.Json.Serialization;

namespace Tool.License
{
    public enum LicenseState
    {
        NotActivated,
        Active,
        Expired,
        Disabled,
        Revoked,
        DeviceRevoked,
        DeviceLimitReached,
        OfflineGrace,
        OfflineExpired,
        InvalidLicense,
        ClockRollbackDetected,
        ServerUnavailable
    }

    public class ApiError
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    public class ApiResponse<T>
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }

        [JsonPropertyName("error")]
        public ApiError? Error { get; set; }
    }

    public class ActivateRequest
    {
        [JsonPropertyName("license_key")]
        public string LicenseKey { get; set; } = string.Empty;

        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonPropertyName("device_name")]
        public string DeviceName { get; set; } = string.Empty;

        [JsonPropertyName("os_name")]
        public string OsName { get; set; } = string.Empty;

        [JsonPropertyName("os_version")]
        public string OsVersion { get; set; } = string.Empty;

        [JsonPropertyName("app_version")]
        public string AppVersion { get; set; } = string.Empty;
    }

    public class ActivateResponseData
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        [JsonPropertyName("device_token")]
        public string DeviceToken { get; set; } = string.Empty;

        [JsonPropertyName("server_time")]
        public DateTime ServerTime { get; set; }

        [JsonPropertyName("offline_grace_hours")]
        public int OfflineGraceHours { get; set; } = 72;

        [JsonPropertyName("product_code")]
        public string ProductCode { get; set; } = string.Empty;

        [JsonPropertyName("signed_license_cache")]
        public string SignedLicenseCache { get; set; } = string.Empty;
    }

    public class ValidateResponseData
    {
        [JsonPropertyName("valid")]
        public bool Valid { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        [JsonPropertyName("server_time")]
        public DateTime ServerTime { get; set; }

        [JsonPropertyName("offline_grace_hours")]
        public int OfflineGraceHours { get; set; } = 72;

        [JsonPropertyName("signed_license_cache")]
        public string SignedLicenseCache { get; set; } = string.Empty;
    }

    public class HeartbeatResponseData
    {
        [JsonPropertyName("acknowledged")]
        public bool Acknowledged { get; set; }

        [JsonPropertyName("server_time")]
        public DateTime ServerTime { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; }
    }

    public class SignedLicensePayload
    {
        [JsonPropertyName("license_id")]
        public string LicenseId { get; set; } = string.Empty;

        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonPropertyName("product_code")]
        public string ProductCode { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        [JsonPropertyName("offline_grace_hours")]
        public int OfflineGraceHours { get; set; } = 72;

        [JsonPropertyName("issued_at")]
        public DateTime IssuedAt { get; set; }
    }

    public class StoredLicenseSession
    {
        public string LicenseKey { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceToken { get; set; } = string.Empty;
        public string SignedLicenseCache { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public DateTime LastSuccessfulValidationUtc { get; set; }
        public DateTime LastRecordedClockUtc { get; set; }
        public int OfflineGraceHours { get; set; } = 72;
        public string ProductCode { get; set; } = string.Empty;
    }
}
