using AutoFixture.Xunit2;
using Bit.Core.Entities;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Models.Business.Tokenables;

public class HCaptchaTokenableTests
{
    [Fact]
    public void CanHandleNullUser()
    {
        var token = new HCaptchaTokenable(null);

        Assert.Equal(default, token.Id);
        Assert.Equal(default, token.Email);
    }

    [Fact]
    public void TokenWithNullUserIsInvalid()
    {
        var token = new HCaptchaTokenable(null)
        {
            ExpirationDate = DateTime.UtcNow + TimeSpan.FromDays(1)
        };

        Assert.False(token.Valid);
    }

    [Theory, BitAutoData]
    public void TokenValidityCheckNullUserIdIsInvalid(User user)
    {
        var token = new HCaptchaTokenable(user)
        {
            ExpirationDate = DateTime.UtcNow + TimeSpan.FromDays(1)
        };

        Assert.False(token.TokenIsValid(null));
    }

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
