using System;
using System.Runtime.Caching;
using System.Security.Cryptography;
using System.Text;

namespace TheArqamSchools.Web.Infrastructure.Auth
{
    /// <summary>
    /// Time-based one-time codes (RFC 6238: 6 digits, 30 seconds, HMAC-SHA1), the
    /// codes shown by Google Authenticator, Microsoft Authenticator and similar apps.
    /// A code is accepted for the current 30-second step and one step either side
    /// (phone clocks drift), and each code can be used only once.
    /// </summary>
    public static class Totp
    {
        private const int Digits = 6;
        private const int StepSeconds = 30;
        private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        private static readonly object ReplayLock = new object();

        /// <summary>A new random 160-bit secret, Base32-encoded (what the app asks for as the "setup key").</summary>
        public static string NewSecret()
        {
            var bytes = new byte[20];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return ToBase32(bytes);
        }

        /// <summary>The secret in groups of four letters, easier to type into the app.</summary>
        public static string Grouped(string secret)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < secret.Length; i++)
            {
                if (i > 0 && i % 4 == 0) sb.Append(' ');
                sb.Append(secret[i]);
            }
            return sb.ToString();
        }

        /// <summary>otpauth:// link: on a phone, tapping it opens the authenticator app with the account filled in.</summary>
        public static string SetupUri(string issuer, string account, string secret)
        {
            var label = Uri.EscapeDataString(issuer) + ":" + Uri.EscapeDataString(account);
            return "otpauth://totp/" + label + "?secret=" + secret + "&issuer=" + Uri.EscapeDataString(issuer)
                + "&algorithm=SHA1&digits=" + Digits + "&period=" + StepSeconds;
        }

        /// <summary>
        /// Checks a code typed by the user. <paramref name="replayKey"/> (for example the
        /// user id) makes each accepted code single-use for that user.
        /// </summary>
        public static bool Verify(string secret, string code, string replayKey)
        {
            if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(code)) return false;
            code = code.Replace(" ", "").Replace("-", "");
            if (code.Length != Digits) return false;
            foreach (var ch in code) if (ch < '0' || ch > '9') return false;

            byte[] key;
            try { key = FromBase32(secret); }
            catch (FormatException) { return false; }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;
            for (long step = now - 1; step <= now + 1; step++)
            {
                if (!ConstantTimeEquals(Compute(key, step), code)) continue;
                lock (ReplayLock)
                {
                    var cacheKey = "totp-used:" + replayKey + ":" + step;
                    if (MemoryCache.Default.Get(cacheKey) != null) return false;
                    MemoryCache.Default.Set(cacheKey, true, DateTimeOffset.UtcNow.AddSeconds(StepSeconds * 4));
                }
                return true;
            }
            return false;
        }

        private static string Compute(byte[] key, long step)
        {
            var counter = BitConverter.GetBytes(step);
            if (BitConverter.IsLittleEndian) Array.Reverse(counter);
            byte[] hash;
            using (var hmac = new HMACSHA1(key)) hash = hmac.ComputeHash(counter);
            var offset = hash[hash.Length - 1] & 0x0F;
            var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
            return (binary % 1000000).ToString("D6");
        }

        private static bool ConstantTimeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        public static string ToBase32(byte[] data)
        {
            var sb = new StringBuilder();
            int buffer = 0, bits = 0;
            foreach (var b in data)
            {
                buffer = (buffer << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    sb.Append(Base32Alphabet[(buffer >> (bits - 5)) & 31]);
                    bits -= 5;
                }
            }
            if (bits > 0) sb.Append(Base32Alphabet[(buffer << (5 - bits)) & 31]);
            return sb.ToString();
        }

        public static byte[] FromBase32(string text)
        {
            var clean = text.Replace(" ", "").TrimEnd('=').ToUpperInvariant();
            var output = new byte[clean.Length * 5 / 8];
            int buffer = 0, bits = 0, index = 0;
            foreach (var ch in clean)
            {
                var value = Base32Alphabet.IndexOf(ch);
                if (value < 0) throw new FormatException("Not a Base32 character.");
                buffer = (buffer << 5) | value;
                bits += 5;
                if (bits >= 8)
                {
                    output[index++] = (byte)((buffer >> (bits - 8)) & 0xFF);
                    bits -= 8;
                }
            }
            return output;
        }
    }
}
