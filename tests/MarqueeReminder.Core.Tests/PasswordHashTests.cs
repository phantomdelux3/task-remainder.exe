using System;
using MarqueeReminder.Core.Security;
using Xunit;

namespace MarqueeReminder.Core.Tests
{
    public class PasswordHashTests
    {
        [Fact]
        public void Verify_AcceptsCorrectPassword_RejectsOthers()
        {
            var hash = PasswordHash.Create("open sesame");

            Assert.True(hash.Verify("open sesame"));
            Assert.False(hash.Verify("Open sesame"));
            Assert.False(hash.Verify("open sesame "));
            Assert.False(hash.Verify(string.Empty));
            Assert.False(hash.Verify(null));
        }

        [Fact]
        public void SamePassword_GetsDifferentSalt()
        {
            Assert.NotEqual(PasswordHash.Create("1234").ToString(), PasswordHash.Create("1234").ToString());
        }

        [Fact]
        public void RoundTripsThroughText()
        {
            var original = PasswordHash.Create("pässwörd");

            Assert.True(PasswordHash.TryParse(original.ToString(), out var parsed));
            Assert.True(parsed!.Verify("pässwörd"));
            Assert.DoesNotContain("pässwörd", original.ToString());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("1234")]
        [InlineData("md5$1$abc$def")]
        [InlineData("pbkdf2-sha256$x$AAAA$AAAA")]
        [InlineData("pbkdf2-sha256$0$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
        [InlineData("pbkdf2-sha256$1000$not-base64!$AAAA")]
        public void TryParse_RejectsMalformedValues(string? value)
        {
            Assert.False(PasswordHash.TryParse(value, out var parsed));
            Assert.Null(parsed);
        }

        [Fact]
        public void Create_RejectsEmptyPassword()
        {
            Assert.Throws<ArgumentException>(() => PasswordHash.Create(string.Empty));
        }

        [Fact]
        public void DefaultForReminders_Is1234()
        {
            Assert.True(PasswordHash.DefaultForReminders.Verify("1234"));
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("123", false)]
        [InlineData("1234", true)]
        [InlineData("a longer pass phrase", true)]
        public void PasswordRules_EnforceMinimumLength(string? password, bool valid)
        {
            Assert.Equal(valid, PasswordRules.Validate(password) == null);
        }

        [Fact]
        public void PasswordRules_EnforceMaximumLength()
        {
            Assert.NotNull(PasswordRules.Validate(new string('x', PasswordRules.MaximumLength + 1)));
            Assert.Null(PasswordRules.Validate(new string('x', PasswordRules.MaximumLength)));
        }
    }
}
