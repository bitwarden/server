using System.Text.Json;
using AutoFixture.Xunit2;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Tokens;

public class ExpiringTokenTests
{
    [Theory, AutoData]
    public void ExpirationSerializesToEpochMilliseconds(DateTime expirationDate)
    {
        var sut = new TestExpiringTokenable
        {
            ExpirationDate = expirationDate
        };

        var result = JsonSerializer.Serialize(sut);
        var expectedDate = CoreHelpers.ToEpocMilliseconds(expirationDate);

        Assert.Contains($"\"ExpirationDate\":{expectedDate}", result);
    }

    [Theory, AutoData]
    public void ExpirationSerializationRoundTrip(DateTime expirationDate)
    {
        var sut = new TestExpiringTokenable
        {
            ExpirationDate = expirationDate
        };

        var intermediate = JsonSerializer.Serialize(sut);
        var result = JsonSerializer.Deserialize<TestExpiringTokenable>(intermediate);

        Assert.Equal(sut.ExpirationDate, result.ExpirationDate, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void InvalidIfPastExpiryDate()
    {
        var sut = new TestExpiringTokenable
        {
            ExpirationDate = DateTime.UtcNow.AddHours(-1)
        };

        Assert.False(sut.Valid);
    }

    [Fact]
    public void ValidIfWithinExpirationAndTokenReportsValid()
    {
        var sut = new TestExpiringTokenable
        {
            ExpirationDate = DateTime.UtcNow.AddHours(1)
        };

        Assert.True(sut.Valid);
    }

    [Fact]
    public void HonorsTokenIsValidAbstractMember()
    {
        var sut = new TestExpiringTokenable(forceInvalid: true)
        {
            ExpirationDate = DateTime.UtcNow.AddHours(1)
        };

        Assert.False(sut.Valid);
    }
}
