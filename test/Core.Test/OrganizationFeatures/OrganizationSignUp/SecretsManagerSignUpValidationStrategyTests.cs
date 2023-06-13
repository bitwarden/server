using AutoFixture.Xunit2;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSignUp;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSignUp;

[SutProviderCustomize]
public class SecretsManagerSignUpValidationStrategyTests
{
    [Theory]
    [BitAutoData]
    public void Validate_WithInvalidAdditionalServiceAccountOption_ThrowsBadRequestException(
        SutProvider<SecretsManagerSignUpValidationStrategy> sutProvider, [Frozen] Plan plan,
        [Frozen] OrganizationUpgrade upgrade)
    {
        plan.HasAdditionalServiceAccountOption = false;
        upgrade.AdditionalServiceAccount = 1;

        Assert.Throws<BadRequestException>(() => sutProvider.Sut.Validate(plan, upgrade));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WithNegativeAdditionalServiceAccount_ThrowsBadRequestException(
        SutProvider<SecretsManagerSignUpValidationStrategy> sutProvider,[Frozen] OrganizationUpgrade upgrade)
    {
        var plan = new Plan { HasAdditionalServiceAccountOption = true, BitwardenProduct = BitwardenProductType.PasswordManager };
        upgrade.AdditionalServiceAccount = -5;

        Assert.Throws<BadRequestException>(() => sutProvider.Sut.Validate(plan, upgrade));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WithZeroSeats_ThrowsBadRequestException(
        SutProvider<SecretsManagerSignUpValidationStrategy> sutProvider
        ,[Frozen] Plan plan,
        [Frozen] OrganizationUpgrade upgrade)
    {
        plan.BaseSeats = 0;
        upgrade.AdditionalSmSeats = -10;

        Assert.Throws<BadRequestException>(() => sutProvider.Sut.Validate(plan, upgrade));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WithInvalidAdditionalSmSeatsOption_ThrowsBadRequestException(
        SutProvider<SecretsManagerSignUpValidationStrategy> sutProvider,
        [Frozen] Plan plan,
        [Frozen] OrganizationUpgrade upgrade)
    {
        plan.HasAdditionalSeatsOption = false;
        upgrade.AdditionalSmSeats = 5;

        Assert.Throws<BadRequestException>(() => sutProvider.Sut.Validate(plan, upgrade));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WithNegativeAdditionalSmSeats_ThrowsBadRequestException(
        SutProvider<SecretsManagerSignUpValidationStrategy> sutProvider,
        [Frozen] OrganizationUpgrade upgrade)
    {
        var plan = new Plan { HasAdditionalSeatsOption = true, BitwardenProduct = BitwardenProductType.PasswordManager };
        upgrade.AdditionalSmSeats = -5;

        Assert.Throws<BadRequestException>(() => sutProvider.Sut.Validate(plan, upgrade));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WithExceededMaxAdditionalSmSeats_ThrowsBadRequestException(
        SutProvider<SecretsManagerSignUpValidationStrategy> sutProvider,
        [Frozen] Plan plan,
        [Frozen] OrganizationUpgrade upgrade)
    {
        plan.HasAdditionalSeatsOption = true;
        plan.MaxAdditionalSeats = 5;
        upgrade.AdditionalSmSeats = 10;

        Assert.Throws<BadRequestException>(() => sutProvider.Sut.Validate(plan, upgrade));
    }
}
