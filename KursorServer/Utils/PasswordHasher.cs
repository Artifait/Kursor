using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace KursorServer.Utils
{
    public static class PasswordHasher
    {
        // PBKDF2 with 100k iterations — fine for prototype
        public static byte[] GenerateSalt(int size = 16) => RandomNumberGenerator.GetBytes(size);

        public static byte[] HashPassword(string password, byte[] salt, int iterations = 100_000, int outBytes = 32)
        {
            using var derive = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            return derive.GetBytes(outBytes);
        }

        public static bool VerifyPassword(string password, byte[] expectedHash, byte[] salt, int iterations = 100_000)
        {
            var h = HashPassword(password, salt, iterations, expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(h, expectedHash);
        }
    }
}
