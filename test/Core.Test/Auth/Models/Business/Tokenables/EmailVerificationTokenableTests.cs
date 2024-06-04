using AutoFixture.Xunit2;

namespace Bit.Core.Test.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Models.Business.Tokenables;
using Xunit;

public class EmailVerificationTokenableTests
{
    // Allow a small tolerance for possible execution delays or clock precision to avoid flaky tests.
    private static readonly TimeSpan _timeTolerance = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Tests the default constructor behavior when passed null/default values.
    /// </summary>
    [Fact]
    public void Constructor_NullInputs_PropertiesSetToDefault()
    {
        var token = new EmailVerificationTokenable(null, null, default);

        Assert.Equal(default, token.Name);
        Assert.Equal(default, token.Email);
        Assert.Equal(default, token.ReceiveMarketingEmails);
    }

    /// <summary>
    /// Tests that when a valid inputs are provided to the constructor, the resulting token properties match the user.
    /// </summary>
    [Theory, AutoData]
    public void Constructor_ValidInputs_PropertiesSetFromInputs(string email, string name, bool receiveMarketingEmails)
    {
        var token = new EmailVerificationTokenable(email, name, receiveMarketingEmails);

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
        var token = new EmailVerificationTokenable();
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
        var token = new EmailVerificationTokenable
        {
            ExpirationDate = customExpiration
        };

        Assert.True((customExpiration - token.ExpirationDate).Duration() < _timeTolerance);
    }

    /// <summary>
    /// Tests the validity of a token initialized with a null / default inputs
    /// </summary>
    [Fact]
    public void Valid_NullInputs_ReturnsFalse()
    {
        var token = new EmailVerificationTokenable(null, null, default);

        Assert.False(token.Valid);
    }

    /// <summary>
    /// Tests the validity of a token with a non-matching identifier.
    /// </summary>
    [Theory, AutoData]
    public void Valid_WrongIdentifier_ReturnsFalse(string email, string name, bool receiveMarketingEmails)
    {
        var token = new EmailVerificationTokenable(email, name, receiveMarketingEmails) { Identifier = "InvalidIdentifier" };

        Assert.False(token.Valid);
    }

    /// <summary>
    /// Tests the token validity when the token is initialized with valid inputs.
    /// </summary>
    [Theory, AutoData]
    public void Valid_ValidInputs_ReturnsTrue(string email, string name, bool receiveMarketingEmails)
    {
        var token = new EmailVerificationTokenable(email, name, receiveMarketingEmails);

        Assert.True(token.Valid);
    }

    // TODO Figure out why this test isn't passing.
    /// <summary>
    /// Tests the token validity when the name is null
    /// </summary>
    [Theory, AutoData]
    public void TokenIsValid_NullName_ReturnsTrue(string email)
    {
        var token = new EmailVerificationTokenable(email, null);

        Assert.True(token.TokenIsValid(email, null));
    }


}
