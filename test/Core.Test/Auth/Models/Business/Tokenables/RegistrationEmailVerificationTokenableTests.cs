using AutoFixture.Xunit2;
using Bit.Core.Tokens;

namespace Bit.Core.Test.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Models.Business.Tokenables;
using Xunit;

public class RegistrationEmailVerificationTokenableTests
{
    // Allow a small tolerance for possible execution delays or clock precision to avoid flaky tests.
    private static readonly TimeSpan _timeTolerance = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Tests the default constructor behavior when passed null/default values.
    /// </summary>
    [Fact]
    public void Constructor_NullEmail_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RegistrationEmailVerificationTokenable(null, null, default));
    }

    /// <summary>
    /// Tests the default constructor behavior when passed required values but null values for optional props.
    /// </summary>
    [Theory, AutoData]
    public void Constructor_NullOptionalProps_PropertiesSetToDefault(string email)
    {
        var token = new RegistrationEmailVerificationTokenable(email, null, default);

        Assert.Equal(email, token.Email);
        Assert.Equal(default, token.Name);
        Assert.Equal(default, token.ReceiveMarketingEmails);
    }

    /// <summary>
    /// Tests that when a valid inputs are provided to the constructor, the resulting token properties match the user.
    /// </summary>
    [Theory, AutoData]
    public void Constructor_ValidInputs_PropertiesSetFromInputs(string email, string name, bool receiveMarketingEmails)
    {
        var token = new RegistrationEmailVerificationTokenable(email, name, receiveMarketingEmails);

        Assert.Equal(email, token.Email);
        Assert.Equal(name, token.Name);
        Assert.Equal(receiveMarketingEmails, token.ReceiveMarketingEmails);
    }

    /// <summary>
    /// Tests the default expiration behavior immediately after initialization.
    /// </summary>
    [Fact]
    public void Constructor_AfterInitialization_ExpirationSetToExpectedDuration()
    {
        var token = new RegistrationEmailVerificationTokenable();
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
        var token = new RegistrationEmailVerificationTokenable
        {
            ExpirationDate = customExpiration
        };

        Assert.True((customExpiration - token.ExpirationDate).Duration() < _timeTolerance);
    }


    /// <summary>
    /// Tests the validity of a token with a non-matching identifier.
    /// </summary>
    [Theory, AutoData]
    public void Valid_WrongIdentifier_ReturnsFalse(string email, string name, bool receiveMarketingEmails)
    {
        var token = new RegistrationEmailVerificationTokenable(email, name, receiveMarketingEmails) { Identifier = "InvalidIdentifier" };

        Assert.False(token.Valid);
    }

    /// <summary>
    /// Tests the token validity when the token is initialized with valid inputs.
    /// </summary>
    [Theory, AutoData]
    public void Valid_ValidInputs_ReturnsTrue(string email, string name, bool receiveMarketingEmails)
    {
        var token = new RegistrationEmailVerificationTokenable(email, name, receiveMarketingEmails);

        Assert.True(token.Valid);
    }

    /// <summary>
    /// Tests the token validity when an incorrect email is provided
    /// </summary>
    [Theory, AutoData]
    public void TokenIsValid_WrongEmail_ReturnsFalse(string email, string name, bool receiveMarketingEmails)
    {
        var token = new RegistrationEmailVerificationTokenable(email, name, receiveMarketingEmails);

        Assert.False(token.TokenIsValid("wrong@email.com"));
    }


    /// <summary>
    /// Tests the deserialization of a token to ensure that the expiration date is preserved.
    /// </summary>
    [Theory, AutoData]
    public void FromToken_SerializedToken_PreservesExpirationDate(string email, string name, bool receiveMarketingEmails)
    {
        var expectedDateTime = DateTime.UtcNow.AddHours(-5);
        var token = new RegistrationEmailVerificationTokenable(email, name, receiveMarketingEmails)
        {
            ExpirationDate = expectedDateTime
        };

        var result = Tokenable.FromToken<RegistrationEmailVerificationTokenable>(token.ToToken());

        Assert.Equal(expectedDateTime, result.ExpirationDate, precision: _timeTolerance);
    }
}
