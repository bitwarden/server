using AutoFixture;
using AutoFixture.Xunit2;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.Models.Business.Tokenables;

// Note: test names follow MethodName_StateUnderTest_ExpectedBehavior pattern.
public class SalesAssistedRegistrationTokenableTests
{
    // Allow a small tolerance for possible execution delays or clock precision to avoid flaky tests.
    private static readonly TimeSpan _timeTolerance = TimeSpan.FromSeconds(10);

    private const int _lifetimeDays = 5;

    private static DataProtectorTokenFactory<SalesAssistedRegistrationTokenable> GetSigningFactory()
    {
        var fixture = new Fixture();
        return new DataProtectorTokenFactory<SalesAssistedRegistrationTokenable>(
            SalesAssistedRegistrationTokenable.ClearTextPrefix,
            SalesAssistedRegistrationTokenable.DataProtectorPurpose,
            fixture.Create<EphemeralDataProtectionProvider>(),
            Substitute.For<ILogger<DataProtectorTokenFactory<SalesAssistedRegistrationTokenable>>>());
    }

    /// <summary>
    /// Tests that a token survives a protect/unprotect round-trip with its data preserved.
    /// </summary>
    [Theory, AutoData]
    public void ProtectUnprotect_ValidToken_PreservesData(string email, string name)
    {
        var factory = GetSigningFactory();
        var token = new SalesAssistedRegistrationTokenable(email, name, _lifetimeDays);

        var protectedToken = factory.Protect(token);
        var recovered = factory.Unprotect(protectedToken);

        Assert.Equal(email, recovered.Email);
        Assert.Equal(name, recovered.Name);
        Assert.Equal(SalesAssistedRegistrationTokenable.TokenIdentifier, recovered.Identifier);
    }

    /// <summary>
    /// Tests that the internal constructor throws when the email is null, empty, or whitespace.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceEmail_Throws(string? email)
    {
        Assert.Throws<ArgumentException>(() =>
            new SalesAssistedRegistrationTokenable(email!, "name", _lifetimeDays));
    }

    /// <summary>
    /// Tests that the constructor sets the expiration date to now plus the supplied lifetime.
    /// </summary>
    [Theory, AutoData]
    public void Constructor_ValidInputs_ExpirationSetToLifetime(string email, string name)
    {
        var expected = DateTime.UtcNow.AddDays(_lifetimeDays);
        var token = new SalesAssistedRegistrationTokenable(email, name, _lifetimeDays);

        Assert.True((expected - token.ExpirationDate).Duration() < _timeTolerance);
    }

    /// <summary>
    /// Tests that TokenIsValid(email) returns true when the email matches.
    /// </summary>
    [Theory, AutoData]
    public void TokenIsValid_MatchingEmail_ReturnsTrue(string email, string name)
    {
        var token = new SalesAssistedRegistrationTokenable(email, name, _lifetimeDays);

        Assert.True(token.TokenIsValid(email));
    }

    /// <summary>
    /// Tests that TokenIsValid(email) is case-insensitive.
    /// </summary>
    [Theory, AutoData]
    public void TokenIsValid_EmailCaseInsensitive_ReturnsTrue(string email, string name)
    {
        var token = new SalesAssistedRegistrationTokenable(email, name, _lifetimeDays);

        Assert.True(token.TokenIsValid(email.ToUpperInvariant()));
    }

    /// <summary>
    /// Tests that TokenIsValid(email) returns false when the email does not match.
    /// </summary>
    [Theory, AutoData]
    public void TokenIsValid_WrongEmail_ReturnsFalse(string email, string name)
    {
        var token = new SalesAssistedRegistrationTokenable(email, name, _lifetimeDays);

        Assert.False(token.TokenIsValid("wrong@example.com"));
    }

    /// <summary>
    /// Tests that a token with a non-matching identifier is invalid.
    /// </summary>
    [Theory, AutoData]
    public void Valid_WrongIdentifier_ReturnsFalse(string email, string name)
    {
        var token = new SalesAssistedRegistrationTokenable(email, name, _lifetimeDays)
        {
            Identifier = "InvalidIdentifier"
        };

        Assert.False(token.Valid);
        Assert.False(token.TokenIsValid(email));
    }

    /// <summary>
    /// Tests that a freshly minted token with a valid email passes the static validator.
    /// </summary>
    [Theory, AutoData]
    public void ValidateSalesAssistedRegistrationToken_ValidTokenAndEmail_ReturnsNull(string email, string name)
    {
        var factory = GetSigningFactory();
        var token = new SalesAssistedRegistrationTokenable(email, name, _lifetimeDays);
        var protectedToken = factory.Protect(token);

        var result = SalesAssistedRegistrationTokenable.ValidateSalesAssistedRegistrationToken(
            factory, protectedToken, email);

        Assert.Null(result);
    }

    // ValidateSalesAssistedRegistrationToken parameters:
    // bool tryUnprotectResult, bool useMatchingEmail, bool isExpired, string? expectedError
    [Theory]
    // TryUnprotect fails → "Invalid token."
    [InlineData(false, true, false, "Invalid token.")]
    // TryUnprotect succeeds, token is expired → "Expired token."
    [InlineData(true, true, true, "Expired token.")]
    // TryUnprotect succeeds, not expired, mismatched email → "Invalid token."
    [InlineData(true, false, false, "Invalid token.")]
    // TryUnprotect succeeds, not expired, matching email → null (valid)
    [InlineData(true, true, false, null)]
    public void ValidateSalesAssistedRegistrationToken_ReturnsExpectedErrors(
        bool tryUnprotectResult,
        bool useMatchingEmail,
        bool isExpired,
        string? expectedError)
    {
        // Arrange
        var email = "test@example.com";

        var tokenable = new SalesAssistedRegistrationTokenable
        {
            Identifier = SalesAssistedRegistrationTokenable.TokenIdentifier,
            Email = email,
            ExpirationDate = isExpired
                ? DateTime.UtcNow.AddDays(-1)
                : DateTime.UtcNow.AddDays(1)
        };

        var factory = Substitute.For<IDataProtectorTokenFactory<SalesAssistedRegistrationTokenable>>();
        factory.TryUnprotect(Arg.Any<string>(), out Arg.Any<SalesAssistedRegistrationTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = tryUnprotectResult ? tokenable : null;
                return tryUnprotectResult;
            });

        var inputEmail = useMatchingEmail ? email : "wrong@example.com";

        // Act
        var result = SalesAssistedRegistrationTokenable.ValidateSalesAssistedRegistrationToken(
            factory, "test-token", inputEmail);

        // Assert
        Assert.Equal(expectedError, result?.ErrorMessage);
    }

    /// <summary>
    /// Tests that the factory applies the configured GlobalSettings lifetime to the minted token's ExpirationDate.
    /// </summary>
    [Theory, AutoData]
    public void Factory_CreateToken_AppliesGlobalSettingsLifetime(string email, string name)
    {
        var globalSettings = new GlobalSettings { SalesAssistedRegistrationTokenLifetimeDays = 7 };
        var sut = new SalesAssistedRegistrationTokenableFactory(globalSettings);

        var expected = DateTime.UtcNow.AddDays(7);
        var token = sut.CreateToken(email, name);

        Assert.Equal(email, token.Email);
        Assert.Equal(name, token.Name);
        Assert.Equal(SalesAssistedRegistrationTokenable.TokenIdentifier, token.Identifier);
        Assert.True((expected - token.ExpirationDate).Duration() < _timeTolerance);
    }
}
