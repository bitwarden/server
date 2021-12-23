using System;
using System.Text.Json;
using AutoFixture.Xunit2;
using Bit.Core.Models.Business;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Tokenizer
{
    public class ExpiringTokenTests
    {
        [Theory, AutoData]
        public void ExpirationSerializesToEpochMilliseconds(DateTime expirationDate)
        {
            var sut = new TestExpiringToken
            {
                ExpirationDate = expirationDate
            };

            var result = JsonSerializer.Serialize(sut);
            var expectedDate = CoreHelpers.ToEpocMilliseconds(expirationDate);

            Assert.Contains($"\"ExpirationDate\":{expectedDate}", result);
        }

        [Fact]
        public void InvalidIfPastExpiryDate()
        {
            var sut = new TestExpiringToken
            {
                ExpirationDate = DateTime.UtcNow.AddHours(-1)
            };

            Assert.False(sut.Valid);
        }

        [Fact]
        public void ValidIfWithinExpirationAndTokenReportsValid()
        {
            var sut = new TestExpiringToken
            {
                ExpirationDate = DateTime.UtcNow.AddHours(1)
            };

            Assert.True(sut.Valid);
        }

        [Fact]
        public void HonorsTokenIsValidAbstractMember()
        {
            var sut = new TestExpiringToken(forceInvalid: true)
            {
                ExpirationDate = DateTime.UtcNow.AddHours(1)
            };

            Assert.False(sut.Valid);
        }
    }
}
