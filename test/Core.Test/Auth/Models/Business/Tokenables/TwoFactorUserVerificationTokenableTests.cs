using AutoFixture.Xunit2;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Tokens;
using Xunit;

namespace Bit.Core.Test.Auth.Models.Business.Tokenables;

public class TwoFactorUserVerificationTokenableTests
{
    [Fact]
    public void DefaultConstructor_NoFactory_ProducesInvalidToken()
    {
        // Locks in the "factory is the only mint path" invariant: a directly-constructed
        // tokenable has ExpirationDate == default and fails Valid.
        var token = new TwoFactorUserVerificationTokenable();

        Assert.False(token.Valid);
    }

    [Theory, AutoData]
    public void Valid_FullyPopulatedNonExpired_ReturnsTrue(User user)
    {
        var token = new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.YubiKey,
            ExpirationDate = DateTime.UtcNow.AddMinutes(5),
        };

        Assert.True(token.Valid);
    }

    [Theory, AutoData]
    public void Valid_ExpiredToken_ReturnsFalse(User user)
    {
        var token = new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.Duo,
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
        };

        Assert.False(token.Valid);
    }

    [Theory, AutoData]
    public void Valid_WrongIdentifier_ReturnsFalse(User user)
    {
        var token = new TwoFactorUserVerificationTokenable
        {
            Identifier = "not the right identifier",
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.Email,
            ExpirationDate = DateTime.UtcNow.AddMinutes(5),
        };

        Assert.False(token.Valid);
    }

    [Fact]
    public void Valid_DefaultUserId_ReturnsFalse()
    {
        var token = new TwoFactorUserVerificationTokenable
        {
            UserId = default,
            ProviderType = TwoFactorProviderType.WebAuthn,
            ExpirationDate = DateTime.UtcNow.AddMinutes(5),
        };

        Assert.False(token.Valid);
    }

    [Theory, AutoData]
    public void TokenIsValid_MatchingUserAndProvider_ReturnsTrue(User user)
    {
        var token = new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.YubiKey,
            ExpirationDate = DateTime.UtcNow.AddMinutes(5),
        };

        Assert.True(token.TokenIsValid(user, TwoFactorProviderType.YubiKey));
    }

    [Theory, AutoData]
    public void TokenIsValid_NullUser_ReturnsFalse(User user)
    {
        var token = new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.YubiKey,
            ExpirationDate = DateTime.UtcNow.AddMinutes(5),
        };

        Assert.False(token.TokenIsValid(null!, TwoFactorProviderType.YubiKey));
    }

    [Theory, AutoData]
    public void TokenIsValid_WrongUser_ReturnsFalse(User user, User otherUser)
    {
        var token = new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.YubiKey,
            ExpirationDate = DateTime.UtcNow.AddMinutes(5),
        };

        Assert.False(token.TokenIsValid(otherUser, TwoFactorProviderType.YubiKey));
    }

    [Theory, AutoData]
    public void TokenIsValid_WrongProviderType_ReturnsFalse(User user)
    {
        var token = new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.Duo,
            ExpirationDate = DateTime.UtcNow.AddMinutes(5),
        };

        Assert.False(token.TokenIsValid(user, TwoFactorProviderType.YubiKey));
    }

    [Theory, AutoData]
    public void FromToken_SerializedToken_PreservesAllFields(User user)
    {
        var expectedExpiration = DateTime.UtcNow.AddMinutes(-3);
        var token = new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.Duo,
            ExpirationDate = expectedExpiration,
        };

        var result = Tokenable.FromToken<TwoFactorUserVerificationTokenable>(token.ToToken());

        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(TwoFactorProviderType.Duo, result.ProviderType);
        Assert.Equal(expectedExpiration, result.ExpirationDate, precision: TimeSpan.FromMilliseconds(10));
    }
}
