using AutoFixture.Xunit2;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Tokens;
using Xunit;

namespace Bit.Core.Test.Auth.Models.Business.Tokenables;

// Note: test names follow MethodName_StateUnderTest_ExpectedBehavior pattern.
public class OrgUserInviteTokenableTests
{
    // Allow a small tolerance for possible execution delays or clock precision.
    private readonly TimeSpan _timeTolerance = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Tests that the default constructor sets the expiration date to the expected duration.
    /// </summary>
    [Fact]
    public void Constructor_DefaultInitialization_ExpirationSetToExpectedDuration()
    {
        var token = new OrgUserInviteTokenable();
        var expectedExpiration = DateTime.UtcNow + OrgUserInviteTokenable.GetTokenLifetime();

        Assert.True(TimesAreCloseEnough(expectedExpiration, token.ExpirationDate, _timeTolerance));
    }

    /// <summary>
    /// Tests that the constructor sets the properties correctly from a valid OrganizationUser object.
    /// </summary>
    [Theory, AutoData]
    public void Constructor_ValidOrgUser_PropertiesSetFromOrgUser(OrganizationUser orgUser)
    {
        var token = new OrgUserInviteTokenable(orgUser);

        Assert.Equal(orgUser.Id, token.OrgUserId);
        Assert.Equal(orgUser.Email, token.OrgUserEmail);
    }

    /// <summary>
    /// Tests that the constructor sets the properties to default values when given a null OrganizationUser object.
    /// </summary>
    [Fact]
    public void Constructor_NullOrgUser_PropertiesSetToDefault()
    {
        var token = new OrgUserInviteTokenable(null);

        Assert.Equal(default, token.OrgUserId);
        Assert.Equal(default, token.OrgUserEmail);
    }

    /// <summary>
    /// Tests that a custom expiration date is preserved after token initialization.
    /// </summary>
    [Fact]
    public void Constructor_CustomExpirationDate_ExpirationMatchesProvidedValue()
    {
        var customExpiration = DateTime.UtcNow.AddHours(3);
        var token = new OrgUserInviteTokenable { ExpirationDate = customExpiration };

        Assert.True(TimesAreCloseEnough(customExpiration, token.ExpirationDate, _timeTolerance));
    }

    /// <summary>
    ///  Tests the validity of a token initialized with a null org user.
    /// </summary>
    [Fact]
    public void Valid_NullOrgUser_ReturnsFalse()
    {
        var token = new OrgUserInviteTokenable(null);

        Assert.False(token.Valid);
    }

    /// <summary>
    /// Tests the validity of a token with a non-matching identifier.
    /// </summary>
    [Fact]
    public void Valid_WrongIdentifier_ReturnsFalse()
    {
        var token = new OrgUserInviteTokenable { Identifier = "IncorrectIdentifier" };

        Assert.False(token.Valid);
    }

    /// <summary>
    /// Tests the validity of the token when the OrgUserId is set to default.
    /// </summary>
    [Fact]
    public void Valid_DefaultOrgUserId_ReturnsFalse()
    {
        var token = new OrgUserInviteTokenable
        {
            OrgUserId = default, // Guid.Empty
        };

        Assert.False(token.Valid);
    }

    /// <summary>
    /// Tests the validity of the token when the OrgUserEmail is null or empty.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Valid_NullOrEmptyOrgUserEmail_ReturnsFalse(string email)
    {
        var token = new OrgUserInviteTokenable { OrgUserEmail = email };

        Assert.False(token.Valid);
    }

    /// <summary>
    /// Tests the validity of the token when the token is expired.
    /// </summary>
    [Fact]
    public void Valid_ExpiredToken_ReturnsFalse()
    {
        var expiredDate = DateTime.UtcNow.AddHours(-3);
        var token = new OrgUserInviteTokenable { ExpirationDate = expiredDate };

        Assert.False(token.Valid);
    }

    /// <summary>
    /// Tests the TokenIsValid method when given a null OrganizationUser object.
    /// </summary>
    [Fact]
    public void TokenIsValid_NullOrgUser_ReturnsFalse()
    {
        var token = new OrgUserInviteTokenable(null);

        Assert.False(token.TokenIsValid(null));
    }

    /// <summary>
    /// Tests the TokenIsValid method when the OrgUserId does not match.
    /// </summary>
    [Theory, AutoData]
    public void TokenIsValid_WrongUserId_ReturnsFalse(OrganizationUser orgUser)
    {
        var token = new OrgUserInviteTokenable(orgUser)
        {
            OrgUserId = Guid.NewGuid(), // Force a different ID
        };

        Assert.False(token.TokenIsValid(orgUser));
    }

    /// <summary>
    /// Tests the TokenIsValid method when the OrgUserEmail does not match.
    /// </summary>
    [Theory, AutoData]
    public void TokenIsValid_WrongEmail_ReturnsFalse(OrganizationUser orgUser)
    {
        var token = new OrgUserInviteTokenable(orgUser)
        {
            OrgUserEmail = "wrongemail@example.com", // Force a different email
        };

        Assert.False(token.TokenIsValid(orgUser));
    }

    /// <summary>
    /// Tests the TokenIsValid method when both OrgUserId and OrgUserEmail match.
    /// </summary>
    [Theory, AutoData]
    public void TokenIsValid_MatchingUserIdAndEmail_ReturnsTrue(OrganizationUser orgUser)
    {
        var token = new OrgUserInviteTokenable(orgUser);

        Assert.True(token.TokenIsValid(orgUser));
    }

    /// <summary>
    /// Tests the TokenIsValid method to ensure email comparison is case-insensitive.
    /// </summary>
    [Theory, AutoData]
    public void TokenIsValid_EmailCaseInsensitiveComparison_ReturnsTrue(OrganizationUser orgUser)
    {
        var token = new OrgUserInviteTokenable(orgUser);

        // Modify the orgUser's email case
        orgUser.Email = orgUser.Email.ToUpperInvariant();

        Assert.True(token.TokenIsValid(orgUser));
    }

    /// <summary>
    /// Tests the TokenIsValid method when the token is expired.
    /// Should return true as TokenIsValid only validates token data -- not token expiration.
    /// </summary>
    [Theory, AutoData]
    public void TokenIsValid_ExpiredToken_ReturnsTrue(OrganizationUser orgUser)
    {
        var expiredDate = DateTime.UtcNow.AddHours(-3);
        var token = new OrgUserInviteTokenable(orgUser) { ExpirationDate = expiredDate };

        Assert.True(token.TokenIsValid(orgUser));
    }

    /// <summary>
    /// Tests the deserialization of a token to ensure that the ExpirationDate is preserved.
    /// </summary>
    [Theory, AutoData]
    public void FromToken_SerializedToken_PreservesExpirationDate(OrganizationUser orgUser)
    {
        // Arbitrary time for testing
        var expectedDateTime = DateTime.UtcNow.AddHours(-3);
        var token = new OrgUserInviteTokenable(orgUser) { ExpirationDate = expectedDateTime };

        var result = Tokenable.FromToken<OrgUserInviteTokenable>(token.ToToken());

        Assert.True(TimesAreCloseEnough(expectedDateTime, result.ExpirationDate, _timeTolerance));
    }

    /// <summary>
    /// Tests the deserialization of a token to ensure that the OrgUserId property is preserved.
    /// </summary>
    [Theory, AutoData]
    public void FromToken_SerializedToken_PreservesOrgUserId(OrganizationUser orgUser)
    {
        var token = new OrgUserInviteTokenable(orgUser);
        var result = Tokenable.FromToken<OrgUserInviteTokenable>(token.ToToken());
        Assert.Equal(orgUser.Id, result.OrgUserId);
    }

    /// <summary>
    /// Tests the deserialization of a token to ensure that the OrgUserEmail property is preserved.
    /// </summary>
    [Theory, AutoData]
    public void FromToken_SerializedToken_PreservesOrgUserEmail(OrganizationUser orgUser)
    {
        var token = new OrgUserInviteTokenable(orgUser);
        var result = Tokenable.FromToken<OrgUserInviteTokenable>(token.ToToken());
        Assert.Equal(orgUser.Email, result.OrgUserEmail);
    }

    private bool TimesAreCloseEnough(DateTime time1, DateTime time2, TimeSpan tolerance)
    {
        return (time1 - time2).Duration() < tolerance;
    }
}
