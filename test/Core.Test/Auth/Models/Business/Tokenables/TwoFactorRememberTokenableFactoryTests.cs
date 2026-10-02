using AutoFixture.Xunit2;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.Models.Business.Tokenables;

public class TwoFactorRememberTokenableFactoryTests
{
    private static readonly DateTime _now = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    private static TwoFactorRememberTokenableFactory GetSut(int lifetimeInDays)
    {
        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.TwoFactorRememberTokenLifetimeInDays.Returns(lifetimeInDays);
        var timeProvider = new FakeTimeProvider();
        timeProvider.SetUtcNow(_now);
        return new TwoFactorRememberTokenableFactory(globalSettings, timeProvider);
    }

    [Theory, AutoData]
    public void CreateToken_BindsUserDeviceAndStamps(User user, Device device)
    {
        var token = GetSut(30).CreateToken(user, device, "row-stamp");

        Assert.Equal(user.Id, token.UserId);
        Assert.Equal(device.Id, token.DeviceId);
        Assert.Equal(device.Identifier, token.DeviceIdentifier);
        Assert.Equal("row-stamp", token.Stamp);
        Assert.Equal(user.SecurityStamp, token.SecurityStamp);
        Assert.True(token.Valid);
    }

    [Theory, AutoData]
    public void CreateToken_HonorsConfiguredLifetime(User user, Device device)
    {
        var token = GetSut(7).CreateToken(user, device, "row-stamp");

        Assert.Equal(_now.AddDays(7), token.ExpirationDate);
    }
}
