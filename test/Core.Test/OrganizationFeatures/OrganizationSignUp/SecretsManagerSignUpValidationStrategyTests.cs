using AutoFixture;
using AutoFixture.Xunit2;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSignUp;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSignUp;

public class SecretsManagerSignUpValidationStrategyTests
{
    [Theory]
    [BitAutoData]
    public void Validate_WithInvalidAdditionalServiceAccountOption_ThrowsBadRequestException(
        [Frozen] Plan plan,
        [Frozen] OrganizationUpgrade upgrade,
        SecretsManagerSignUpValidationStrategy sut)
    {
        // Arrange
        plan.HasAdditionalServiceAccountOption = false;
        upgrade.AdditionalServiceAccount = 1;

        // Act & Assert
        Assert.Throws<BadRequestException>(() => sut.Validate(plan, upgrade));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WithNegativeAdditionalServiceAccount_ThrowsBadRequestException(
        [Frozen] OrganizationUpgrade upgrade,
        SecretsManagerSignUpValidationStrategy strategy)
    {
        // Arrange
        var plan = new Plan { HasAdditionalServiceAccountOption = true, BitwardenProduct = BitwardenProductType.PasswordManager };
        upgrade.AdditionalServiceAccount = -5;

        Assert.Throws<BadRequestException>(() => strategy.Validate(plan, upgrade));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WithZeroSeats_ThrowsBadRequestException(
        [Frozen] Plan plan,
        [Frozen] OrganizationUpgrade upgrade,
        SecretsManagerSignUpValidationStrategy sut)
    {
        // Arrange
        plan.BaseSeats = 0;
        upgrade.AdditionalSmSeats = -10;

        // Act & Assert
        Assert.Throws<BadRequestException>(() => sut.Validate(plan, upgrade));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WithInvalidAdditionalSmSeatsOption_ThrowsBadRequestException(
        [Frozen] Plan plan,
        [Frozen] OrganizationUpgrade upgrade,
        SecretsManagerSignUpValidationStrategy sut)
    {
        // Arrange
        plan.HasAdditionalSeatsOption = false;
        upgrade.AdditionalSmSeats = 5;

        // Act & Assert
        Assert.Throws<BadRequestException>(() => sut.Validate(plan, upgrade));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WithNegativeAdditionalSmSeats_ThrowsBadRequestException(
        [Frozen] OrganizationUpgrade upgrade,
        SecretsManagerSignUpValidationStrategy strategy)
    {
        // Arrange
        var plan = new Plan { HasAdditionalSeatsOption = true, BitwardenProduct = BitwardenProductType.PasswordManager };
        upgrade.AdditionalSmSeats = -5;

        // Act & Assert
        Assert.Throws<BadRequestException>(() => strategy.Validate(plan, upgrade));
    }

    [Theory]
    [BitAutoData]
    public void Validate_WithExceededMaxAdditionalSmSeats_ThrowsBadRequestException(
        [Frozen] Plan plan,
        [Frozen] OrganizationUpgrade upgrade,
        SecretsManagerSignUpValidationStrategy sut)
    {
        // Arrange
        plan.HasAdditionalSeatsOption = true;
        plan.MaxAdditionalSeats = 5;
        upgrade.AdditionalSmSeats = 10;

        // Act & Assert
        Assert.Throws<BadRequestException>(() => sut.Validate(plan, upgrade));
    }

    private static Plan CreatePlan()
    {
        var fixture = new Fixture();
        fixture.Customize<Plan>(composer =>
        {
            // Customize properties of the Plan object
            composer.With(p => p.HasAdditionalServiceAccountOption, true);
            composer.With(p => p.HasAdditionalSeatsOption, true);
            composer.With(p => p.MaxAdditionalSeats, 10);
            composer.With(p => p.BitwardenProduct, BitwardenProductType.SecretsManager);
            return null;
        });
        return fixture.Create<Plan>();
    }

    private static OrganizationUpgrade CreateUpgrade()
    {
        var fixture = new Fixture();
        fixture.Customize<OrganizationUpgrade>(composer =>
        {
            composer.With(ou => ou.AdditionalServiceAccount, 3);
            composer.With(ou => ou.AdditionalSmSeats, 5);
            composer.With(ou => ou.UseSecretsManager, true);
            return null;
        });


        return fixture.Create<OrganizationUpgrade>();
    }
}
