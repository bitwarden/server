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
}
