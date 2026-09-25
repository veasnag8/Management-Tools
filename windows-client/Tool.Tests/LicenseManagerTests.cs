using System;
using System.Threading;
using System.Threading.Tasks;
using Tool.License;
using Tool.Services;
using Xunit;

namespace Tool.Tests
{
    public class MockStorageService : ISecureStorageService
    {
        public StoredLicenseSession? Session { get; set; }

        public StoredLicenseSession? LoadSession() => Session;
        public void SaveSession(StoredLicenseSession session) => Session = session;
        public void ClearSession() => Session = null;
    }

    public class MockDeviceIdService : IDeviceIdService
    {
        public string GetDeviceId() => "PC-12345678-87654321";
        public string GetDeviceName() => "Test-PC";
        public string GetOsName() => "Windows";
        public string GetOsVersion() => "10.0.22631";
    }

    public class MockClockService : IClockService
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        public bool SimulateRollback { get; set; } = false;

        public DateTime GetCurrentUtc() => UtcNow;

        public bool IsClockRollback(DateTime lastRecordedUtc, out TimeSpan rollbackAmount)
        {
            if (SimulateRollback)
            {
                rollbackAmount = TimeSpan.FromHours(2);
                return true;
            }
            rollbackAmount = TimeSpan.Zero;
            return false;
        }
    }

    public class MockApiClient : ILicenseApiClient
    {
        public ApiResponse<ActivateResponseData> ActivateResult { get; set; } = new() { Success = true, Data = new() { DeviceToken = "jwt.token.123", OfflineGraceHours = 72, Status = "active" } };
        public ApiResponse<ValidateResponseData> ValidateResult { get; set; } = new() { Success = true, Data = new() { Valid = true, Status = "active", OfflineGraceHours = 72 } };

        public Task<ApiResponse<ActivateResponseData>> ActivateAsync(string lk, string di, string dn, string os, string osv, string av, CancellationToken ct) => Task.FromResult(ActivateResult);
        public Task<ApiResponse<ValidateResponseData>> ValidateAsync(string dt, string di, string av, CancellationToken ct) => Task.FromResult(ValidateResult);
        public Task<ApiResponse<HeartbeatResponseData>> HeartbeatAsync(string dt, string av, CancellationToken ct) => Task.FromResult(new ApiResponse<HeartbeatResponseData> { Success = true, Data = new() { Acknowledged = true } });
        public Task<ApiResponse<object>> DeactivateAsync(string dt, string r, CancellationToken ct) => Task.FromResult(new ApiResponse<object> { Success = true });
        public Task<ApiResponse<Updates.VersionResponseData>> GetVersionAsync(string p, string v, CancellationToken ct) => Task.FromResult(new ApiResponse<Updates.VersionResponseData> { Success = true, Data = new() { CurrentVersion = "1.0.0", LatestVersion = "1.0.0", HasUpdate = false } });
    }

    public class LicenseManagerTests
    {
        [Fact]
        public async Task Startup_NoSession_ReturnsNotActivated()
        {
            var storage = new MockStorageService();
            var manager = new LicenseManager(
                new MockApiClient(),
                storage,
                new MockDeviceIdService(),
                new LicenseSignatureVerifier(),
                new MockClockService()
            );

            var state = await manager.InitializeAndValidateAsync();
            Assert.Equal(LicenseState.NotActivated, state);
        }

        [Fact]
        public async Task Startup_ValidSession_ReturnsActive()
        {
            var storage = new MockStorageService
            {
                Session = new StoredLicenseSession
                {
                    DeviceToken = "valid.jwt.token",
                    DeviceId = "PC-12345678-87654321",
                    LicenseKey = "TOOL-AAAAAA-BBBBBB-CCCC"
                }
            };

            var manager = new LicenseManager(
                new MockApiClient(),
                storage,
                new MockDeviceIdService(),
                new LicenseSignatureVerifier(),
                new MockClockService()
            );

            var state = await manager.InitializeAndValidateAsync();
            Assert.Equal(LicenseState.Active, state);
        }

        [Fact]
        public async Task Startup_ClockRollback_Detected()
        {
            var storage = new MockStorageService
            {
                Session = new StoredLicenseSession
                {
                    DeviceToken = "valid.jwt.token",
                    DeviceId = "PC-12345678-87654321",
                    LastRecordedClockUtc = new DateTime(2026, 1, 1, 14, 0, 0, DateTimeKind.Utc)
                }
            };

            var clock = new MockClockService { SimulateRollback = true };
            var manager = new LicenseManager(
                new MockApiClient(),
                storage,
                new MockDeviceIdService(),
                new LicenseSignatureVerifier(),
                clock
            );

            var state = await manager.InitializeAndValidateAsync();
            Assert.Equal(LicenseState.ClockRollbackDetected, state);
        }
    }
}
