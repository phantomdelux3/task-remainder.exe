namespace MarqueeReminder.Core.Security
{
    /// <summary>What counts as an acceptable password (for both reminders and the app).</summary>
    public static class PasswordRules
    {
        public const int MinimumLength = 4;
        public const int MaximumLength = 128;

        /// <summary>Returns a message describing what is wrong, or null if the password is acceptable.</summary>
        public static string? Validate(string? password)
        {
            if (string.IsNullOrEmpty(password) || password!.Length < MinimumLength)
            {
                return $"The password must be at least {MinimumLength} characters long.";
            }

            if (password.Length > MaximumLength)
            {
                return $"The password can be at most {MaximumLength} characters long.";
            }

            return null;
        }
    }
}
