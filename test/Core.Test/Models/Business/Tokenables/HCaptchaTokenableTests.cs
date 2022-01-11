using System;
using AutoFixture.Xunit2;
using Bit.Core.Entities;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.Tokens;
using Xunit;

namespace Bit.Core.Test.Models.Business.Tokenables
{
    public class HCaptchaTokenableTests
    {
        [Theory, AutoData]
        public void CanUpdateExpirationToNonStandard(User user)
        {
            var token = new HCaptchaTokenable(user)
            {
                ExpirationDate = DateTime.MinValue
            };

            Assert.Equal(DateTime.MinValue, token.ExpirationDate, TimeSpan.FromMilliseconds(10));
        }

        [Theory, AutoData]
        public void SetsDataFromUser(User user)
        {
            var token = new HCaptchaTokenable(user);

            Assert.Equal(user.Id, token.Id);
            Assert.Equal(user.Email, token.Email);
        }

        [Theory, AutoData]
        public void SerializationSetsCorrectDateTime(User user)
        {
            var expectedDateTime = DateTime.UtcNow.AddHours(-5);
            var token = new HCaptchaTokenable(user)
            {
                ExpirationDate = expectedDateTime
            };

            var result = Tokenable.FromToken<HCaptchaTokenable>(token.ToToken());

            Assert.Equal(expectedDateTime, result.ExpirationDate, TimeSpan.FromMilliseconds(10));
        }

        [Theory, AutoData]
        public void IsInvalidIfIdentifierIsWrong(User user)
        {
            var token = new HCaptchaTokenable(user)
            {
                Identifier = "not correct"
            };

            Assert.False(token.Valid);
        }
    }
}
