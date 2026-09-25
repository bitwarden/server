using AutoFixture.Xunit2;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Tokens;
using Xunit;

namespace Bit.Core.Test.Auth.Models.Business.Tokenables;

public class TwoFactorAuthenticatorUserVerificationTokenableTests
{
    [Theory, AutoData]
    public void Constructor_ValidInputs_PropertiesSetFromInputs(User user, string key)
    {
        var token = new TwoFactorAuthenticatorUserVerificationTokenable(user, key);

        Assert.Equal(user.Id, token.UserId);
        Assert.Equal(key, token.Key);
        Assert.Equal(TwoFactorAuthenticatorUserVerificationTokenable.TokenIdentifier, token.Identifier);
    }

    [Theory, AutoData]
    public void Constructor_NullUser_UserIdDefault(string key)
    {
        var token = new TwoFactorAuthenticatorUserVerificationTokenable(null, key);

        Assert.Equal(default, token.UserId);
        Assert.Equal(key, token.Key);
    }

    [Fact]
    public void Constructor_AfterInitialization_ExpirationSetToExpectedDuration()
    {
        var before = DateTime.UtcNow;
        var token = new TwoFactorAuthenticatorUserVerificationTokenable();
        var after = DateTime.UtcNow;

        Assert.InRange(
            token.ExpirationDate,
            before + TimeSpan.FromMinutes(30),
            after + TimeSpan.FromMinutes(30));
    }

    [Theory, AutoData]
    public void Valid_NewlyCreatedToken_ReturnsTrue(User user, string key)
    {
        var token = new TwoFactorAuthenticatorUserVerificationTokenable(user, key);

        Assert.True(token.Valid);
    }

    [Theory, AutoData]
    public void Valid_ExpiredToken_ReturnsFalse(User user, string key)
    {
        var token = new TwoFactorAuthenticatorUserVerificationTokenable(user, key)
        {
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1)
        };

        Assert.False(token.Valid);
    }

    [Theory, AutoData]
    public void Valid_WrongIdentifier_ReturnsFalse(User user, string key)
    {
        var token = new TwoFactorAuthenticatorUserVerificationTokenable(user, key)
        {
            Identifier = "not correct"
        };

        Assert.False(token.Valid);
    }

    [Theory, AutoData]
    public void Valid_DefaultUserId_ReturnsFalse(string key)
    {
        var token = new TwoFactorAuthenticatorUserVerificationTokenable(null, key);

        Assert.False(token.Valid);
    }

    [Theory, AutoData]
    public void Valid_EmptyKey_ReturnsFalse(User user)
    {
        var token = new TwoFactorAuthenticatorUserVerificationTokenable(user, "");

        Assert.False(token.Valid);
    }

    [Theory, AutoData]
    public void TokenIsValid_MatchingUserAndKey_ReturnsTrue(User user, string key)
    {
        var token = new TwoFactorAuthenticatorUserVerificationTokenable(user, key);

        Assert.True(token.TokenIsValid(user, key));
    }

    [Theory, AutoData]
    public void TokenIsValid_NullUser_ReturnsFalse(User user, string key)
    {
        var token = new TwoFactorAuthenticatorUserVerificationTokenable(user, key);

        Assert.False(token.TokenIsValid(null, key));
    }

    [Theory, AutoData]
    public void TokenIsValid_WrongUserId_ReturnsFalse(User user, User otherUser, string key)
    {
        var token = new TwoFactorAuthenticatorUserVerificationTokenable(user, key);

        Assert.False(token.TokenIsValid(otherUser, key));
    }

    [Theory, AutoData]
    public void TokenIsValid_WrongKey_ReturnsFalse(User user, string key)
    {
        var token = new TwoFactorAuthenticatorUserVerificationTokenable(user, key);

        Assert.False(token.TokenIsValid(user, "different-key"));
    }

    [Theory, AutoData]
    public void TokenIsValid_EmptyProvidedKey_ReturnsFalse(User user, string key)
    {
        var token = new TwoFactorAuthenticatorUserVerificationTokenable(user, key);

        Assert.False(token.TokenIsValid(user, ""));
    }

    // TokenIsValid(User, string) is a data-only check; expiration is the
    // caller's responsibility via the Valid property. This documents that
    // contract.
    [Theory, AutoData]
    public void TokenIsValid_ExpiredToken_ReturnsTrue(User user, string key)
    {
        var token = new TwoFactorAuthenticatorUserVerificationTokenable(user, key)
        {
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1)
        };

        Assert.True(token.TokenIsValid(user, key));
    }

    [Theory, AutoData]
    public void FromToken_SerializedToken_PreservesExpirationDate(User user, string key)
    {
        var expectedDateTime = DateTime.UtcNow.AddMinutes(-5);
        var token = new TwoFactorAuthenticatorUserVerificationTokenable(user, key)
        {
            ExpirationDate = expectedDateTime
        };

        var result = Tokenable.FromToken<TwoFactorAuthenticatorUserVerificationTokenable>(token.ToToken());

        Assert.Equal(expectedDateTime, result.ExpirationDate, precision: TimeSpan.FromMilliseconds(10));
    }
}
