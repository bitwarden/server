using AutoFixture.Xunit2;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSignUp;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Core.Exceptions;
using Xunit;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSignUp;

[SutProviderCustomize]
public class PasswordManagerSignUpValidationStrategyTests
{
    [Theory]
    [BitAutoData]
    public void Validate_WhenPlanDoesNotAllowAdditionalStorageAndUpgradeRequestsAdditionalStorage_ThrowsBadRequestException(OrganizationUpgrade upgrade,
        PasswordManagerSignUpValidationStrategy strategy)
    {
        var plan = new Plan { HasAdditionalStorageOption = false, BitwardenProduct = BitwardenProductType.PasswordManager };
        upgrade.AdditionalStorageGb = 10;

        Assert.Throws<BadRequestException>(() => strategy.Validate(plan, upgrade));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenUpgradeRequestsNegativeAdditionalStorage_ThrowsBadRequestException(
        [Frozen] OrganizationUpgrade upgrade,
        PasswordManagerSignUpValidationStrategy strategy)
    {
        var plan = new Plan { HasAdditionalStorageOption = true, BitwardenProduct = BitwardenProductType.PasswordManager };
        upgrade.AdditionalStorageGb = -5;

        Assert.Throws<BadRequestException>(() => strategy.Validate(plan, upgrade));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenNoSeatsAfterUpgrade_ThrowsBadRequestException(
        [Frozen] Plan plan,
        [Frozen] OrganizationUpgrade upgrade,
        PasswordManagerSignUpValidationStrategy strategy)
    {
        plan.BaseSeats = 5;
        plan.BitwardenProduct = BitwardenProductType.PasswordManager;
        upgrade.AdditionalSeats = -5;

        Assert.Throws<BadRequestException>(() => strategy.Validate(plan, upgrade));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenUpgradeRequestsNegativeAdditionalSeats_ThrowsBadRequestException(
        [Frozen] Plan plan,
        [Frozen] OrganizationUpgrade upgrade,
        PasswordManagerSignUpValidationStrategy strategy)
    {
        plan.BitwardenProduct = BitwardenProductType.PasswordManager;
        upgrade.AdditionalSeats = -3;

        Assert.Throws<BadRequestException>(() => strategy.Validate(plan, upgrade));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenPlanDoesNotAllowAdditionalSeatsAndUpgradeRequestsAdditionalSeats_ThrowsBadRequestException(
        [Frozen] Plan plan,
        [Frozen] OrganizationUpgrade upgrade,
        PasswordManagerSignUpValidationStrategy strategy)
    {
        plan.HasAdditionalSeatsOption = false;
        plan.BitwardenProduct = BitwardenProductType.PasswordManager;
        upgrade.AdditionalSeats = 2;

        Assert.Throws<BadRequestException>(() => strategy.Validate(plan, upgrade));
    }

}
