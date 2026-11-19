using System;
using System.Security.Cryptography;

namespace TheArqamSchools.Web.Infrastructure.Auth
{
    /// <summary>
    /// Password hashing for admin users with PBKDF2-HMAC-SHA256 (built into .NET
    /// Framework 4.8, no extra package). Stored as
    /// "PBKDF2-SHA256$iterations$salt$hash" (salt and hash in Base64), so the
    /// iteration count can be raised later without breaking existing passwords.
    /// </summary>
    public static class PasswordHasher
    {
        private const string Scheme = "PBKDF2-SHA256";
        private const int Iterations = 100000;
        private const int SaltBytes = 16;
        private const int HashBytes = 32;

        public const int MinLength = 12;
        public const int MaxLength = 128;

        /// <summary>Used when the email is unknown, so a wrong email takes as long as a wrong password.</summary>
        private static readonly Lazy<string> DummyHash = new Lazy<string>(() => Hash("not-a-real-password-" + Guid.NewGuid()));

        public static string Hash(string password)
        {
            if (password == null) throw new ArgumentNullException("password");
            var salt = new byte[SaltBytes];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            var hash = Derive(password, salt, Iterations);
            return Scheme + "$" + Iterations + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
        }

        /// <summary>True when the password matches. A missing or malformed hash never matches.</summary>
        public static bool Verify(string password, string stored)
        {
            if (password == null) return false;
            if (string.IsNullOrEmpty(stored))
            {
                Verify(password, DummyHash.Value);
                return false;
            }
            var parts = stored.Split('$');
            int iterations;
            if (parts.Length != 4 || parts[0] != Scheme || !int.TryParse(parts[1], out iterations) || iterations < 10000)
                return false;
            byte[] salt, expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }
            var actual = Derive(password, salt, iterations, expected.Length);
            return FixedTimeEquals(actual, expected);
        }

        /// <summary>Spend the same time as a real check when the account does not exist.</summary>
        public static void VerifyDummy(string password)
        {
            Verify(password ?? "", DummyHash.Value);
        }

        public static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static byte[] Derive(string password, byte[] salt, int iterations, int length = HashBytes)
        {
            using (var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                return kdf.GetBytes(length);
        }
    }
}
