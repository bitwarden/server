using AutoFixture.Xunit2;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Settings;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.Models.Business.Tokenables;

public class TwoFactorUserVerificationTokenableFactoryTests
{
    [Theory, AutoData]
    public void CreateToken_BindsUserAndProvider(User user)
    {
        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.TwoFactorUserVerificationTokenLifetimeInMinutes.Returns(30);
        var sut = new TwoFactorUserVerificationTokenableFactory(globalSettings);

        var token = sut.CreateToken(user, TwoFactorProviderType.Duo);

        Assert.Equal(user.Id, token.UserId);
        Assert.Equal(TwoFactorProviderType.Duo, token.ProviderType);
        Assert.True(token.Valid);
    }

    [Theory, AutoData]
    public void CreateToken_HonorsConfiguredLifetime(User user)
    {
        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.TwoFactorUserVerificationTokenLifetimeInMinutes.Returns(15);
        var sut = new TwoFactorUserVerificationTokenableFactory(globalSettings);

        var before = DateTime.UtcNow;
        var token = sut.CreateToken(user, TwoFactorProviderType.YubiKey);
        var after = DateTime.UtcNow;

        Assert.InRange(
            token.ExpirationDate,
            before + TimeSpan.FromMinutes(15),
            after + TimeSpan.FromMinutes(15));
    }
}
