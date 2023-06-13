using AutoFixture.Xunit2;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSignUp;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Core.Exceptions;
using Xunit;
using Bit.Core.Models.StaticStore;
using Bit.Test.Common.AutoFixture;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSignUp;

[SutProviderCustomize]
public class PasswordManagerSignUpValidationStrategyTests
{
    [Theory]
    [BitAutoData]
    public void Validate_WhenPlanDoesNotAllowAdditionalStorageAndUpgradeRequestsAdditionalStorage_ThrowsBadRequestException(
        SutProvider<PasswordManagerSignUpValidationStrategy> sutProvider,OrganizationUpgrade upgrade)
    {
        var plan = new Plan { HasAdditionalStorageOption = false, BitwardenProduct = BitwardenProductType.PasswordManager };
        upgrade.AdditionalStorageGb = 10;

        Assert.Throws<BadRequestException>(() => sutProvider.Sut.Validate(plan, upgrade));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenUpgradeRequestsNegativeAdditionalStorage_ThrowsBadRequestException(
        SutProvider<PasswordManagerSignUpValidationStrategy> sutProvider,
        [Frozen] OrganizationUpgrade upgrade)
    {
        var plan = new Plan { HasAdditionalStorageOption = true, BitwardenProduct = BitwardenProductType.PasswordManager };
        upgrade.AdditionalStorageGb = -5;

        Assert.Throws<BadRequestException>(() => sutProvider.Sut.Validate(plan, upgrade));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenNoSeatsAfterUpgrade_ThrowsBadRequestException(
        SutProvider<PasswordManagerSignUpValidationStrategy> sutProvider,
        [Frozen] Plan plan,
        [Frozen] OrganizationUpgrade upgrade)
    {
        plan.BaseSeats = 5;
        plan.BitwardenProduct = BitwardenProductType.PasswordManager;
        upgrade.AdditionalSeats = -5;

        Assert.Throws<BadRequestException>(() => sutProvider.Sut.Validate(plan, upgrade));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenUpgradeRequestsNegativeAdditionalSeats_ThrowsBadRequestException(
        SutProvider<PasswordManagerSignUpValidationStrategy> sutProvider,
        [Frozen] Plan plan,
        [Frozen] OrganizationUpgrade upgrade)
    {
        plan.BitwardenProduct = BitwardenProductType.PasswordManager;
        upgrade.AdditionalSeats = -3;

        Assert.Throws<BadRequestException>(() => sutProvider.Sut.Validate(plan, upgrade));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenPlanDoesNotAllowAdditionalSeatsAndUpgradeRequestsAdditionalSeats_ThrowsBadRequestException(
        SutProvider<PasswordManagerSignUpValidationStrategy> sutProvider,
        [Frozen] Plan plan,
        [Frozen] OrganizationUpgrade upgrade)
    {
        plan.HasAdditionalSeatsOption = false;
        plan.BitwardenProduct = BitwardenProductType.PasswordManager;
        upgrade.AdditionalSeats = 2;

        Assert.Throws<BadRequestException>(() => sutProvider.Sut.Validate(plan, upgrade));
    }

}
