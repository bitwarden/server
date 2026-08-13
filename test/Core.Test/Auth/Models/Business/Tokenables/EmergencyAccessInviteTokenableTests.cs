using AutoFixture.Xunit2;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Tokens;
using Xunit;

namespace Bit.Core.Test.Auth.Models.Business.Tokenables;

public class EmergencyAccessInviteTokenableTests
{
    [Theory, AutoData]
    public void SerializationSetsCorrectDateTime(EmergencyAccess emergencyAccess)
    {
        var token = new EmergencyAccessInviteTokenable(emergencyAccess, 2);
        Assert.Equal(Tokenable.FromToken<EmergencyAccessInviteTokenable>(token.ToToken().ToString()).ExpirationDate,
            token.ExpirationDate,
            TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void IsInvalidIfIdentifierIsWrong()
    {
        var token = new EmergencyAccessInviteTokenable(DateTime.MaxValue)
        {
            Email = "email",
            Id = Guid.NewGuid(),
            Identifier = "not correct"
        };

        Assert.False(token.Valid);
    }

    [Theory, AutoData]
    public void Valid_NotExpired_ReturnsTrue(EmergencyAccess emergencyAccess)
    {
        var token = new EmergencyAccessInviteTokenable(emergencyAccess, 1);

        Assert.True(token.Valid);
    }

    [Theory, AutoData]
    public void Valid_ExpiredToken_ReturnsFalse(EmergencyAccess emergencyAccess)
    {
        var token = new EmergencyAccessInviteTokenable(emergencyAccess, -1);

        Assert.False(token.Valid);
    }

    // IsValid(Guid, string) is a data-only check; expiration is the caller's
    // responsibility via the Valid property. This documents that contract.
    [Theory, AutoData]
    public void IsValid_ExpiredToken_ReturnsTrue(EmergencyAccess emergencyAccess)
    {
        var token = new EmergencyAccessInviteTokenable(emergencyAccess, -1);

        Assert.True(token.IsValid(emergencyAccess.Id, emergencyAccess.Email));
    }
}
