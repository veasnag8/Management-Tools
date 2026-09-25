using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Tool.License
{
    public interface ISecureStorageService
    {
        StoredLicenseSession? LoadSession();
        void SaveSession(StoredLicenseSession session);
        void ClearSession();
    }

    public class SecureStorageService : ISecureStorageService
    {
        private readonly string _storageFilePath;
        private readonly object _lock = new();
        private static readonly byte[] EntropySalt = Encoding.UTF8.GetBytes("LicensePlatform_DPAPI_v1");

        public SecureStorageService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "LicensePlatform");
            Directory.CreateDirectory(folder);
            _storageFilePath = Path.Combine(folder, "session.dat");
        }

        public StoredLicenseSession? LoadSession()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_storageFilePath))
                    {
                        return null;
                    }

                    var encryptedBytes = File.ReadAllBytes(_storageFilePath);
                    if (encryptedBytes.Length == 0) return null;

                    var decryptedBytes = ProtectedData.Unprotect(
                        encryptedBytes,
                        EntropySalt,
                        DataProtectionScope.CurrentUser
                    );

                    var json = Encoding.UTF8.GetString(decryptedBytes);
                    return JsonSerializer.Deserialize<StoredLicenseSession>(json);
                }
                catch
                {
                    return null;
                }
            }
        }

        public void SaveSession(StoredLicenseSession session)
        {
            lock (_lock)
            {
                try
                {
                    var json = JsonSerializer.Serialize(session);
                    var plainBytes = Encoding.UTF8.GetBytes(json);

                    var encryptedBytes = ProtectedData.Protect(
                        plainBytes,
                        EntropySalt,
                        DataProtectionScope.CurrentUser
                    );

                    File.WriteAllBytes(_storageFilePath, encryptedBytes);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to write secure session: {ex.Message}");
                }
            }
        }

        public void ClearSession()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_storageFilePath))
                    {
                        File.Delete(_storageFilePath);
                    }
                }
                catch { }
            }
        }
    }
}
