using System.ComponentModel.DataAnnotations;
using Bit.Api.KeyManagement.Models.Requests;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Models.Request;

public class OrganizationUserV2UpgradesRequestModelTests
{
    private const string _validUserKeyId = "0123456789abcdef0123456789abcdef";
    private const string _validAccountRecoveryKey =
        "2.BPt52Ie9PQjDQYkzKLDjEB==|P7PIiu3V3iKHCTOHojnKnh==|jE44t9C79D9KiZZiTb5W2uBskwMs9fFbHrPW8CSp6Kl=";

    [Fact]
    public void Validate_WithOneUpgrade_ReturnsNoErrors()
    {
        // Arrange
        var model = ModelFor(Upgrade(Guid.NewGuid()));

        // Act
        var results = Validate(model);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WithNoUpgrades_ReturnsValidationError()
    {
        // Arrange
        var model = ModelFor();

        // Act
        var results = Validate(model);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.Upgrades)));
    }

    [Fact]
    public void Validate_WithTwoUpgradesForOneMembership_ReturnsValidationError()
    {
        // Arrange - two updates for one membership would make the outcome depend on write order
        var organizationUserId = Guid.NewGuid();
        var model = ModelFor(Upgrade(organizationUserId), Upgrade(organizationUserId));

        // Act
        var results = Validate(model);

        // Assert
        Assert.Single(results);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.Upgrades)));
        Assert.Contains(results,
            r => r.ErrorMessage == "An organization user can only be upgraded once per request.");
    }

    [Fact]
    public void Validate_WithTwoUpgradesForDifferentMemberships_ReturnsNoErrors()
    {
        // Arrange
        var model = ModelFor(Upgrade(Guid.NewGuid()), Upgrade(Guid.NewGuid()));

        // Act
        var results = Validate(model);

        // Assert
        Assert.Empty(results);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-hex")]
    [InlineData("0123456789abcdef")]                          // too short
    [InlineData("0123456789abcdef0123456789abcdef0")]         // too long
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]          // uppercase
    public void Validate_WithMalformedUserKeyId_ReturnsValidationError(string userKeyId)
    {
        // Arrange
        var upgrade = new OrganizationUserV2UpgradeRequestModel
        {
            OrganizationUserId = Guid.NewGuid(),
            UserKeyId = userKeyId,
            AccountRecoveryKey = _validAccountRecoveryKey
        };

        // Act
        var results = Validate(upgrade);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(upgrade.UserKeyId)));
    }

    [Fact]
    public void Validate_WithoutAnAccountRecoveryKey_ReturnsNoErrors()
    {
        // Arrange - a null key unenrolls the member, which clears an upgrade that cannot be completed
        var upgrade = new OrganizationUserV2UpgradeRequestModel
        {
            OrganizationUserId = Guid.NewGuid(),
            UserKeyId = _validUserKeyId,
            AccountRecoveryKey = null
        };

        // Act
        var results = Validate(upgrade);

        // Assert
        Assert.Empty(results);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-encrypted-string")]
    public void Validate_WithAccountRecoveryKeyThatIsNotAnEncryptedString_ReturnsValidationError(
        string accountRecoveryKey)
    {
        // Arrange - a blank key is not a way to unenroll, only a null key is
        var upgrade = new OrganizationUserV2UpgradeRequestModel
        {
            OrganizationUserId = Guid.NewGuid(),
            UserKeyId = _validUserKeyId,
            AccountRecoveryKey = accountRecoveryKey
        };

        // Act
        var results = Validate(upgrade);

        // Assert
        Assert.Single(results);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(upgrade.AccountRecoveryKey)));
        Assert.Contains(results,
            r => r.ErrorMessage == "AccountRecoveryKey is not a valid encrypted string.");
    }

    [Fact]
    public void ToData_MapsEveryUpgrade()
    {
        // Arrange
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var model = ModelFor(Upgrade(firstId), Upgrade(secondId));

        // Act
        var data = model.ToData().ToList();

        // Assert
        Assert.Equal(2, data.Count);
        Assert.Equal(firstId, data[0].OrganizationUserId);
        Assert.Equal(secondId, data[1].OrganizationUserId);
        Assert.All(data, update =>
        {
            Assert.Equal(_validUserKeyId, update.UserKeyId);
            Assert.Equal(_validAccountRecoveryKey, update.AccountRecoveryKey);
        });
    }

    [Fact]
    public void ToData_WithoutAnAccountRecoveryKey_MapsTheKeyToNull()
    {
        // Arrange - a null key unenrolls the member
        var model = ModelFor(new OrganizationUserV2UpgradeRequestModel
        {
            OrganizationUserId = Guid.NewGuid(),
            UserKeyId = _validUserKeyId,
            AccountRecoveryKey = null
        });

        // Act
        var data = model.ToData().ToList();

        // Assert
        Assert.Null(Assert.Single(data).AccountRecoveryKey);
    }

    private static OrganizationUserV2UpgradeRequestModel Upgrade(Guid organizationUserId) =>
        new()
        {
            OrganizationUserId = organizationUserId,
            UserKeyId = _validUserKeyId,
            AccountRecoveryKey = _validAccountRecoveryKey
        };

    private static OrganizationUserV2UpgradesRequestModel ModelFor(
        params OrganizationUserV2UpgradeRequestModel[] upgrades) =>
        new() { Upgrades = upgrades };

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}
