using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tool.License;
using Xunit;

namespace Tool.Tests
{
    public class SignatureVerifierTests
    {
        private static string Base64UrlEncode(byte[] input)
        {
            var output = Convert.ToBase64String(input);
            output = output.Split('=')[0];
            output = output.Replace('+', '-').Replace('/', '_');
            return output;
        }

        [Fact]
        public void VerifyAndDecodeCache_ValidSignature_ReturnsTrue()
        {
            var verifier = new LicenseSignatureVerifier();
            var secretKey = "test-secret-key-1234567890123456";

            var payload = new SignedLicensePayload
            {
                LicenseId = Guid.NewGuid().ToString(),
                DeviceId = "PC-7F92KQ81-XP48MD9R",
                ProductCode = "TEST-TOOL",
                Status = "active",
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                OfflineGraceHours = 72,
                IssuedAt = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(payload);
            var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(json));

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            var sigBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(encodedPayload));
            var encodedSig = Base64UrlEncode(sigBytes);

            var signedToken = $"{encodedPayload}.{encodedSig}";

            var isValid = verifier.VerifyAndDecodeCache(signedToken, secretKey, out var decoded);

            Assert.True(isValid);
            Assert.NotNull(decoded);
            Assert.Equal("TEST-TOOL", decoded.ProductCode);
            Assert.Equal("PC-7F92KQ81-XP48MD9R", decoded.DeviceId);
        }

        [Fact]
        public void VerifyAndDecodeCache_TamperedPayload_ReturnsFalse()
        {
            var verifier = new LicenseSignatureVerifier();
            var secretKey = "test-secret-key-1234567890123456";

            var signedToken = "eyJnZW51aW5lIjpmYWxzZX0.invalidSignature123";

            var isValid = verifier.VerifyAndDecodeCache(signedToken, secretKey, out var decoded);

            Assert.False(isValid);
            Assert.Null(decoded);
        }
    }
}
