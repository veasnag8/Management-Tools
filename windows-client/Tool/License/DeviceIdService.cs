using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace Tool.License
{
    public interface IDeviceIdService
    {
        string GetDeviceId();
        string GetDeviceName();
        string GetOsName();
        string GetOsVersion();
    }

    public class DeviceIdService : IDeviceIdService
    {
        private static string? _cachedDeviceId;

        public string GetDeviceId()
        {
            if (!string.IsNullOrEmpty(_cachedDeviceId))
            {
                return _cachedDeviceId;
            }

            var rawFingerprint = CollectMachineFingerprint();
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawFingerprint));
            var hex = Convert.ToHexString(hashBytes);

            var part1 = hex.Substring(0, 8);
            var part2 = hex.Substring(8, 8);
            _cachedDeviceId = $"PC-{part1}-{part2}";

            return _cachedDeviceId;
        }

        public string GetDeviceName()
        {
            try { return Environment.MachineName; }
            catch { return "Windows-Desktop"; }
        }

        public string GetOsName() => "Windows";

        public string GetOsVersion() => Environment.OSVersion.VersionString;

        private string CollectMachineFingerprint()
        {
            var sb = new StringBuilder();

            try
            {
                using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                if (key != null)
                {
                    var machineGuid = key.GetValue("MachineGuid")?.ToString();
                    if (!string.IsNullOrEmpty(machineGuid))
                    {
                        sb.Append($"MachineGuid:{machineGuid};");
                    }
                }
            }
            catch { }

            sb.Append($"Arch:{Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")};");
            sb.Append($"Cores:{Environment.ProcessorCount};");

            try
            {
                var sysDir = Environment.SystemDirectory;
                var driveInfo = new DriveInfo(Path.GetPathRoot(sysDir) ?? "C:\\");
                sb.Append($"Volume:{driveInfo.VolumeLabel};Format:{driveInfo.DriveFormat};");
            }
            catch
            {
                sb.Append("Volume:Default;");
            }

            sb.Append($"Domain:{Environment.UserDomainName};");
            return sb.ToString();
        }
    }
}
