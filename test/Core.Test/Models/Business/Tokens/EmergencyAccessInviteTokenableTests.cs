using System;
using AutoFixture.Xunit2;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.Models.Table;
using Bit.Core.Tokens;
using Xunit;

namespace Bit.Core.Test.Tokenizer
{
    public class EmergencyAccessInviteTokenTests
    {
        [Theory, AutoData]
        public void SerializationSetsCorrectDateTime(EmergencyAccess emergencyAccess)
        {
            var token = new EmergencyAccessInviteToken(emergencyAccess, 2);
            Assert.Equal(Tokenable.FromToken<EmergencyAccessInviteToken>(token.ToToken().ToString()).ExpirationDate,
                token.ExpirationDate,
                TimeSpan.FromMilliseconds(10));
        }

        [Fact]
        public void IsInvalidIfIdentifierIsWrong()
        {
            var token = new EmergencyAccessInviteToken(DateTime.MaxValue)
            {
                Email = "email",
                Id = Guid.NewGuid(),
                Identifier = "not correct"
            };

            Assert.False(token.Valid);
        }
    }
}
