using AutoFixture.Xunit2;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Tokens;
using Xunit;

namespace Bit.Core.Test.Auth.Models.Business.Tokenables;

// Note: these test names follow MethodName_StateUnderTest_ExpectedBehavior pattern.
public class SsoEmail2faSessionTokenableTests
{
    // Allow a small tolerance for possible execution delays or clock precision to avoid flaky tests.
    private static readonly TimeSpan _timeTolerance = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Tests the default constructor behavior when passed a null user.
    /// </summary>
    [Fact]
    public void Constructor_NullUser_PropertiesSetToDefault()
    {
        var token = new SsoEmail2faSessionTokenable(null);

        Assert.Equal(default, token.Id);
        Assert.Equal(default, token.Email);
    }

    /// <summary>
    /// Tests that when a valid user is provided to the constructor, the resulting token properties match the user.
    /// </summary>
    [Theory, AutoData]
    public void Constructor_ValidUser_PropertiesSetFromUser(User user)
    {
        var token = new SsoEmail2faSessionTokenable(user);

        Assert.Equal(user.Id, token.Id);
        Assert.Equal(user.Email, token.Email);
    }

    /// <summary>
    /// Tests the default expiration behavior immediately after initialization.
    /// </summary>
    [Fact]
    public void Constructor_AfterInitialization_ExpirationSetToExpectedDuration()
    {
        var token = new SsoEmail2faSessionTokenable();
        var expectedExpiration = DateTime.UtcNow + SsoEmail2faSessionTokenable.GetTokenLifetime();

        Assert.True(expectedExpiration - token.ExpirationDate < _timeTolerance);
    }

    /// <summary>
    /// Tests that a custom expiration date is preserved after token initialization.
    /// </summary>
    [Fact]
    public void Constructor_CustomExpirationDate_ExpirationMatchesProvidedValue()
    {
        var customExpiration = DateTime.UtcNow.AddHours(3);
        var token = new SsoEmail2faSessionTokenable { ExpirationDate = customExpiration };

        Assert.True((customExpiration - token.ExpirationDate).Duration() < _timeTolerance);
    }

    /// <summary>
    /// Tests the validity of a token initialized with a null user.
    /// </summary>
    [Fact]
    public void Valid_NullUser_ReturnsFalse()
    {
        var token = new SsoEmail2faSessionTokenable(null);

        Assert.False(token.Valid);
    }

    /// <summary>
    /// Tests the validity of a token with a non-matching identifier.
    /// </summary>
    [Theory, AutoData]
    public void Valid_WrongIdentifier_ReturnsFalse(User user)
    {
        var token = new SsoEmail2faSessionTokenable(user) { Identifier = "not correct" };

        Assert.False(token.Valid);
    }

    /// <summary>
    /// Tests the token validity when user ID is null.
    /// </summary>
    [Theory, AutoData]
    public void TokenIsValid_NullUserId_ReturnsFalse(User user)
    {
        user.Id = default; // Guid.Empty
        var token = new SsoEmail2faSessionTokenable(user);

        Assert.False(token.TokenIsValid(user));
    }

    /// <summary>
    /// Tests the token validity when user's email is null.
    /// </summary>
    [Theory, AutoData]
    public void TokenIsValid_NullEmail_ReturnsFalse(User user)
    {
        user.Email = null;
        var token = new SsoEmail2faSessionTokenable(user);

        Assert.False(token.TokenIsValid(user));
    }

    /// <summary>
    /// Tests the token validity when user ID and email match the token properties.
    /// </summary>
    [Theory, AutoData]
    public void TokenIsValid_MatchingUserIdAndEmail_ReturnsTrue(User user)
    {
        var token = new SsoEmail2faSessionTokenable(user);

        Assert.True(token.TokenIsValid(user));
    }

    /// <summary>
    /// Ensures that the token is invalid when the provided user's ID doesn't match the token's ID.
    /// </summary>
    [Theory, AutoData]
    public void TokenIsValid_WrongUserId_ReturnsFalse(User user)
    {
        // Given a token initialized with a user's details
        var token = new SsoEmail2faSessionTokenable(user);

        // modify the user's ID
        user.Id = Guid.NewGuid();

        // Then the token should be considered invalid
        Assert.False(token.TokenIsValid(user));
    }

    /// <summary>
    /// Ensures that the token is invalid when the provided user's email doesn't match the token's email.
    /// </summary>
    [Theory, AutoData]
    public void TokenIsValid_WrongEmail_ReturnsFalse(User user)
    {
        // Given a token initialized with a user's details
        var token = new SsoEmail2faSessionTokenable(user);

        // modify the user's email
        user.Email = "nonMatchingEmail@example.com";

        // Then the token should be considered invalid
        Assert.False(token.TokenIsValid(user));
    }

    /// <summary>
    /// Tests the deserialization of a token to ensure that the expiration date is preserved.
    /// </summary>
    [Theory, AutoData]
    public void FromToken_SerializedToken_PreservesExpirationDate(User user)
    {
        var expectedDateTime = DateTime.UtcNow.AddHours(-5);
        var token = new SsoEmail2faSessionTokenable(user) { ExpirationDate = expectedDateTime };

        var result = Tokenable.FromToken<SsoEmail2faSessionTokenable>(token.ToToken());

        Assert.Equal(expectedDateTime, result.ExpirationDate, precision: _timeTolerance);
    }
}
