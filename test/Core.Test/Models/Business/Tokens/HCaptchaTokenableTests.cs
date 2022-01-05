using System;
using System.Text.Json;
using AutoFixture.Xunit2;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.Models.Table;
using Bit.Core.Tokens;
using Xunit;

namespace Bit.Core.Test.Models.Business.Tokens
{
    public class HCaptchaTokenTests
    {
        [Theory, AutoData]
        public void CanUpdateExpirationToNonStandard(User user)
        {
            var token = new HCaptchaToken(user)
            {
                ExpirationDate = DateTime.MinValue
            };

            Assert.Equal(DateTime.MinValue, token.ExpirationDate, TimeSpan.FromMilliseconds(10));
        }

        [Theory, AutoData]
        public void SetsDataFromUser(User user)
        {
            var token = new HCaptchaToken(user);

            Assert.Equal(user.Id, token.Id);
            Assert.Equal(user.Email, token.Email);
        }

        [Theory, AutoData]
        public void SerializationSetsCorrectDateTime(User user)
        {
            var expectedDateTime = DateTime.UtcNow.AddHours(-5);
            var token = new HCaptchaToken(user)
            {
                ExpirationDate = expectedDateTime
            };

            var result = Tokenable.FromToken<HCaptchaToken>(token.ToToken());

            Assert.Equal(expectedDateTime, result.ExpirationDate, TimeSpan.FromMilliseconds(10));
        }

        [Theory, AutoData]
        public void IsInvalidIfIdentifierIsWrong(User user)
        {
            var token = new HCaptchaToken(user)
            {
                Identifier = "not correct"
            };

            Assert.False(token.Valid);
        }
    }
}
