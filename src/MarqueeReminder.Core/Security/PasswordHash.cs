using System;
using System.Globalization;
using System.Security.Cryptography;

namespace MarqueeReminder.Core.Security
{
    /// <summary>
    /// A salted PBKDF2-SHA256 password hash. Passwords are never stored in plain text; only this
    /// hash is saved, in the form <c>pbkdf2-sha256$iterations$salt$hash</c>. Instances are immutable.
    /// </summary>
    public sealed class PasswordHash
    {
        public const string DefaultReminderPassword = "1234";

        private const string Scheme = "pbkdf2-sha256";
        private const int DefaultIterations = 100_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        private static readonly Lazy<PasswordHash> DefaultReminder =
            new Lazy<PasswordHash>(() => Create(DefaultReminderPassword));

        private readonly int _iterations;
        private readonly byte[] _salt;
        private readonly byte[] _hash;

        private PasswordHash(int iterations, byte[] salt, byte[] hash)
        {
            _iterations = iterations;
            _salt = salt;
            _hash = hash;
        }

        /// <summary>The hash of <see cref="DefaultReminderPassword"/>, computed once and shared.</summary>
        public static PasswordHash DefaultForReminders => DefaultReminder.Value;

        public static PasswordHash Create(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password cannot be empty.", nameof(password));
            }

            var salt = new byte[SaltSize];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(salt);
            }

            return new PasswordHash(DefaultIterations, salt, Derive(password, salt, DefaultIterations));
        }

        public static bool TryParse(string? value, out PasswordHash? result)
        {
            result = null;
            var parts = value?.Split('$');
            if (parts == null || parts.Length != 4 || parts[0] != Scheme
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations)
                || iterations < 1)
            {
                return false;
            }

            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var hash = Convert.FromBase64String(parts[3]);
                if (salt.Length != SaltSize || hash.Length != HashSize)
                {
                    return false;
                }

                result = new PasswordHash(iterations, salt, hash);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public bool Verify(string? password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return false;
            }

            return FixedTimeEquals(_hash, Derive(password!, _salt, _iterations));
        }

        public override string ToString() =>
            string.Join("$", Scheme, _iterations.ToString(CultureInfo.InvariantCulture), Convert.ToBase64String(_salt), Convert.ToBase64String(_hash));

        private static byte[] Derive(string password, byte[] salt, int iterations)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(HashSize);
            }
        }

        // Compares every byte so the time taken reveals nothing about where a guess went wrong.
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            var difference = a.Length ^ b.Length;
            for (var i = 0; i < a.Length && i < b.Length; i++)
            {
                difference |= a[i] ^ b[i];
            }

            return difference == 0;
        }
    }
}
