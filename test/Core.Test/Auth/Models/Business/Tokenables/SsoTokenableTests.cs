using AutoFixture.Xunit2;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Auth.Models.Business.Tokenables;

public class SsoTokenableTests
{
    [Fact]
    public void CanHandleNullOrganization()
    {
        var token = new SsoTokenable(null, default);

        Assert.Equal(default, token.OrganizationId);
        Assert.Equal(default, token.DomainHint);
    }

    [Fact]
    public void TokenWithNullOrganizationIsInvalid()
    {
        var token = new SsoTokenable(null, 500)
        {
            ExpirationDate = DateTime.UtcNow + TimeSpan.FromDays(1)
        };

        Assert.False(token.Valid);
    }

    [Theory, BitAutoData]
    public void TokenValidityCheckNullOrganizationIsInvalid(Organization organization)
    {
        var token = new SsoTokenable(organization, 500)
        {
            ExpirationDate = DateTime.UtcNow + TimeSpan.FromDays(1)
        };

        Assert.False(token.TokenIsValid(null));
    }

    [Theory, AutoData]
    public void SetsDataFromOrganization(Organization organization)
    {
        var token = new SsoTokenable(organization, default);

        Assert.Equal(organization.Id, token.OrganizationId);
        Assert.Equal(organization.Identifier, token.DomainHint);
    }

    [Fact]
    public void SetsExpirationFromConstructor()
    {
        var expectedDateTime = DateTime.UtcNow.AddSeconds(500);
        var token = new SsoTokenable(null, 500);

        Assert.Equal(expectedDateTime, token.ExpirationDate, TimeSpan.FromMilliseconds(10));
    }

    [Theory, AutoData]
    public void SerializationSetsCorrectDateTime(Organization organization)
    {
        var expectedDateTime = DateTime.UtcNow.AddHours(-5);
        var token = new SsoTokenable(organization, default)
        {
            ExpirationDate = expectedDateTime
        };

        var result = Tokenable.FromToken<HCaptchaTokenable>(token.ToToken());

        Assert.Equal(expectedDateTime, result.ExpirationDate, TimeSpan.FromMilliseconds(10));
    }

    [Theory, AutoData]
    public void TokenIsValidFailsWhenExpired(Organization organization)
    {
        var expectedDateTime = DateTime.UtcNow.AddHours(-5);
        var token = new SsoTokenable(organization, default)
        {
            ExpirationDate = expectedDateTime
        };

        var result = token.TokenIsValid(organization);

        Assert.False(result);
    }
}
