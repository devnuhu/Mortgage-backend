using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace MMGT.API.Utils
{
    public static class SecurityHelpers
    {
        public static string GenerateUrlSafeToken(int bytes = 48)
        {
            var data = new byte[bytes];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(data);
            return WebEncoders.Base64UrlEncode(data);
        }

        public static string HashToken(string token)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
#if NET5_0_OR_GREATER
            return Convert.ToHexString(hash);
#else
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
#endif
        }
    }
}