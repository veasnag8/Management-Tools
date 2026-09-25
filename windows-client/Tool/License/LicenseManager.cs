using System;
using System.Threading;
using System.Threading.Tasks;
using Tool.Services;

namespace Tool.License
{
    public interface ILicenseManager
    {
        LicenseState CurrentState { get; }
        StoredLicenseSession? CurrentSession { get; }
        string CurrentDeviceId { get; }
        string? LastErrorMessage { get; }
        event EventHandler<LicenseState>? StateChanged;

        Task<LicenseState> InitializeAndValidateAsync(CancellationToken ct = default);
        Task<bool> ActivateLicenseAsync(string licenseKey, CancellationToken ct = default);
        Task<bool> DeactivateLicenseAsync(CancellationToken ct = default);
    }

    public class LicenseManager : ILicenseManager, IDisposable
    {
        private readonly ILicenseApiClient _apiClient;
        private readonly ISecureStorageService _storageService;
        private readonly IDeviceIdService _deviceIdService;
        private readonly ILicenseSignatureVerifier _signatureVerifier;
        private readonly IClockService _clockService;
        private readonly string _verificationKey;

        private Timer? _heartbeatTimer;
        private readonly object _lock = new();

        public LicenseState CurrentState { get; private set; } = LicenseState.NotActivated;
        public StoredLicenseSession? CurrentSession { get; private set; }
        public string CurrentDeviceId => _deviceIdService.GetDeviceId();
        public string? LastErrorMessage { get; private set; }

        public event EventHandler<LicenseState>? StateChanged;

        public LicenseManager(
            ILicenseApiClient apiClient,
            ISecureStorageService storageService,
            IDeviceIdService deviceIdService,
            ILicenseSignatureVerifier signatureVerifier,
            IClockService clockService,
            string verificationKey = "default-fallback-super-secret-key-32b")
        {
            _apiClient = apiClient;
            _storageService = storageService;
            _deviceIdService = deviceIdService;
            _signatureVerifier = signatureVerifier;
            _clockService = clockService;
            _verificationKey = verificationKey;
        }

        public async Task<LicenseState> InitializeAndValidateAsync(CancellationToken ct = default)
        {
            var session = _storageService.LoadSession();
            if (session == null || string.IsNullOrWhiteSpace(session.DeviceToken))
            {
                SetState(LicenseState.NotActivated, "No activated license found on this PC.");
                return CurrentState;
            }

            CurrentSession = session;
            var deviceId = _deviceIdService.GetDeviceId();
            var appVersion = "1.0.0";

            if (_clockService.IsClockRollback(session.LastRecordedClockUtc, out var rollback))
            {
                SetState(LicenseState.ClockRollbackDetected, $"System clock tampering detected (shifted back by {rollback.TotalMinutes:F1} min).");
                return CurrentState;
            }

            var res = await _apiClient.ValidateAsync(session.DeviceToken, deviceId, appVersion, ct);

            if (res.Success && res.Data != null && res.Data.Valid)
            {
                var now = _clockService.GetCurrentUtc();
                session.LastSuccessfulValidationUtc = now;
                session.LastRecordedClockUtc = now;
                session.ExpiresAt = res.Data.ExpiresAt;
                session.OfflineGraceHours = res.Data.OfflineGraceHours;
                session.SignedLicenseCache = res.Data.SignedLicenseCache;

                _storageService.SaveSession(session);
                CurrentSession = session;

                StartHeartbeatTimer();
                SetState(LicenseState.Active, null);
                return CurrentState;
            }

            if (res.Error != null && !string.Equals(res.Error.Code, "NETWORK_ERROR", StringComparison.OrdinalIgnoreCase)
                                 && !string.Equals(res.Error.Code, "TIMEOUT", StringComparison.OrdinalIgnoreCase))
            {
                var errorCode = res.Error.Code.ToUpperInvariant();
                if (errorCode.Contains("EXPIRED"))
                {
                    SetState(LicenseState.Expired, res.Error.Message);
                }
                else if (errorCode.Contains("REVOKED") || errorCode.Contains("DEVICE_REVOKED"))
                {
                    SetState(LicenseState.DeviceRevoked, res.Error.Message);
                }
                else if (errorCode.Contains("DISABLED"))
                {
                    SetState(LicenseState.Disabled, res.Error.Message);
                }
                else
                {
                    SetState(LicenseState.InvalidLicense, res.Error.Message);
                }
                return CurrentState;
            }

            return EvaluateOfflineGracePeriod(session);
        }

        public async Task<bool> ActivateLicenseAsync(string licenseKey, CancellationToken ct = default)
        {
            var cleanKey = licenseKey.Trim().ToUpperInvariant();
            var deviceId = _deviceIdService.GetDeviceId();
            var deviceName = _deviceIdService.GetDeviceName();
            var osName = _deviceIdService.GetOsName();
            var osVersion = _deviceIdService.GetOsVersion();
            var appVersion = "1.0.0";

            var res = await _apiClient.ActivateAsync(
                cleanKey,
                deviceId,
                deviceName,
                osName,
                osVersion,
                appVersion,
                ct);

            if (res.Success && res.Data != null)
            {
                var now = _clockService.GetCurrentUtc();
                var session = new StoredLicenseSession
                {
                    LicenseKey = cleanKey,
                    DeviceId = deviceId,
                    DeviceToken = res.Data.DeviceToken,
                    SignedLicenseCache = res.Data.SignedLicenseCache,
                    ExpiresAt = res.Data.ExpiresAt,
                    LastSuccessfulValidationUtc = now,
                    LastRecordedClockUtc = now,
                    OfflineGraceHours = res.Data.OfflineGraceHours,
                    ProductCode = res.Data.ProductCode
                };

                _storageService.SaveSession(session);
                CurrentSession = session;

                StartHeartbeatTimer();
                SetState(LicenseState.Active, null);
                return true;
            }

            var errCode = res.Error?.Code ?? "INVALID_REQUEST";
            var errMsg = res.Error?.Message ?? "License activation failed.";

            if (errCode.Contains("DEVICE_LIMIT"))
            {
                SetState(LicenseState.DeviceLimitReached, errMsg);
            }
            else if (errCode.Contains("EXPIRED"))
            {
                SetState(LicenseState.Expired, errMsg);
            }
            else if (errCode.Contains("DISABLED"))
            {
                SetState(LicenseState.Disabled, errMsg);
            }
            else
            {
                SetState(LicenseState.InvalidLicense, errMsg);
            }

            return false;
        }

        public async Task<bool> DeactivateLicenseAsync(CancellationToken ct = default)
        {
            if (CurrentSession != null && !string.IsNullOrEmpty(CurrentSession.DeviceToken))
            {
                await _apiClient.DeactivateAsync(CurrentSession.DeviceToken, "Client user initiated deactivation", ct);
            }

            StopHeartbeatTimer();
            _storageService.ClearSession();
            CurrentSession = null;
            SetState(LicenseState.NotActivated, "License deactivated.");
            return true;
        }

        private LicenseState EvaluateOfflineGracePeriod(StoredLicenseSession session)
        {
            if (!_signatureVerifier.VerifyAndDecodeCache(session.SignedLicenseCache, _verificationKey, out var payload)
                || payload == null)
            {
                SetState(LicenseState.InvalidLicense, "Offline authorization cache corrupted or invalid signature.");
                return CurrentState;
            }

            var now = _clockService.GetCurrentUtc();

            if (payload.ExpiresAt.HasValue && now > payload.ExpiresAt.Value)
            {
                SetState(LicenseState.Expired, "License entitlement has expired.");
                return CurrentState;
            }

            var graceSpan = TimeSpan.FromHours(session.OfflineGraceHours > 0 ? session.OfflineGraceHours : 72);
            var offlineElapsed = now - session.LastSuccessfulValidationUtc;

            if (offlineElapsed <= graceSpan)
            {
                session.LastRecordedClockUtc = now;
                _storageService.SaveSession(session);

                var remainingHours = (int)(graceSpan - offlineElapsed).TotalHours;
                SetState(LicenseState.OfflineGrace, $"Offline Mode: {remainingHours} hours grace remaining.");
                return CurrentState;
            }

            SetState(LicenseState.OfflineExpired, "Offline grace period has elapsed. Please connect to the Internet to validate.");
            return CurrentState;
        }

        private void StartHeartbeatTimer()
        {
            StopHeartbeatTimer();
            _heartbeatTimer = new Timer(async _ => await PerformHeartbeatAsync(), null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
        }

        private void StopHeartbeatTimer()
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
        }

        private async Task PerformHeartbeatAsync()
        {
            if (CurrentSession == null || string.IsNullOrEmpty(CurrentSession.DeviceToken)) return;

            try
            {
                var res = await _apiClient.HeartbeatAsync(CurrentSession.DeviceToken, "1.0.0");
                if (res.Success && res.Data != null)
                {
                    CurrentSession.LastRecordedClockUtc = _clockService.GetCurrentUtc();
                    _storageService.SaveSession(CurrentSession);
                }
            }
            catch { }
        }

        private void SetState(LicenseState state, string? error)
        {
            lock (_lock)
            {
                CurrentState = state;
                LastErrorMessage = error;
            }
            StateChanged?.Invoke(this, state);
        }

        public void Dispose()
        {
            StopHeartbeatTimer();
        }
    }
}
