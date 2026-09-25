using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Tool.License
{
    public interface ILicenseSignatureVerifier
    {
        bool VerifyAndDecodeCache(string signedCache, string verificationKey, out SignedLicensePayload? payload);
    }

    public class LicenseSignatureVerifier : ILicenseSignatureVerifier
    {
        public bool VerifyAndDecodeCache(
            string signedCache,
            string verificationKey,
            out SignedLicensePayload? payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(signedCache) || string.IsNullOrWhiteSpace(verificationKey))
            {
                return false;
            }

            var parts = signedCache.Split('.');
            if (parts.Length != 2)
            {
                return false;
            }

            var encodedPayload = parts[0];
            var encodedSignature = parts[1];

            try
            {
                var keyBytes = Encoding.UTF8.GetBytes(verificationKey);
                using var hmac = new HMACSHA256(keyBytes);
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(encodedPayload));
                var expectedSignature = Base64UrlEncode(computedHash);

                if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(expectedSignature),
                    Encoding.UTF8.GetBytes(encodedSignature)))
                {
                    return false;
                }

                var payloadBytes = Base64UrlDecode(encodedPayload);
                var json = Encoding.UTF8.GetString(payloadBytes);
                payload = JsonSerializer.Deserialize<SignedLicensePayload>(json);

                return payload != null;
            }
            catch
            {
                payload = null;
                return false;
            }
        }

        private static string Base64UrlEncode(byte[] input)
        {
            var output = Convert.ToBase64String(input);
            output = output.Split('=')[0];
            output = output.Replace('+', '-').Replace('/', '_');
            return output;
        }

        private static byte[] Base64UrlDecode(string input)
        {
            var output = input.Replace('-', '+').Replace('_', '/');
            switch (output.Length % 4)
            {
                case 0: break;
                case 2: output += "=="; break;
                case 3: output += "="; break;
                default: throw new ArgumentOutOfRangeException(nameof(input), "Illegal base64url string!");
            }
            return Convert.FromBase64String(output);
        }
    }
}
