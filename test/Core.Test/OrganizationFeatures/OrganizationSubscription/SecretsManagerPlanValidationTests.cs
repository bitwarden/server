using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSmSubscription;
using Bit.Core.OrganizationFeatures.OrganizationSubscription;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSmSubscription;
[SutProviderCustomize]
public class SecretsManagerPlanValidationTests
{
    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenInvalidPlanSelected(PlanType planType, SutProvider<SecretsManagerPlanValidation> sutProvider)
    {

        var plan = new Plan { LegacyYear = 2000, Type = planType };
        var signup = new Organization();
        var additionalSeats = 1;
        var additionalServiceAccounts = 0;

        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup, additionalSeats, additionalServiceAccounts));
        Assert.Contains("Invalid Secrets Manager plan selected.", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenPlanIsDisabled(PlanType planType, SutProvider<SecretsManagerPlanValidation> sutProvider)
    {
        var plan = new Plan { Disabled = true, Type = planType };
        var signup = new Organization();
        var additionalSeats = 10;
        var additionalServiceAccounts = 5;

        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup, additionalSeats, additionalServiceAccounts));
        Assert.Contains("Secrets Manager Plan not found.", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenNoSecretsManagerSeats(PlanType planType, SutProvider<SecretsManagerPlanValidation> sutProvider)
    {
        var plan = new Plan { BaseSeats = 0, Type = planType };
        var signup = new Organization();
        var additionalSeats = 0;
        var additionalServiceAccounts = 5;

        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup, additionalSeats, additionalServiceAccounts));
        Assert.Contains("You do not have any Secrets Manager seats!", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenSubtractingSeats(PlanType planType, SutProvider<SecretsManagerPlanValidation> sutProvider)
    {
        var plan = new Plan { BaseSeats = 10, Type = planType };
        var signup = new Organization();
        var additionalSeats = -5;
        var additionalServiceAccounts = 5;

        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup, additionalSeats, additionalServiceAccounts));
        Assert.Contains("You can't subtract Secrets Manager seats!", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenPlanDoesNotAllowAdditionalServiceAccounts(PlanType planType, SutProvider<SecretsManagerPlanValidation> sutProvider)
    {
        var plan = new Plan { HasAdditionalServiceAccountOption = false, Type = planType };
        var signup = new Organization();
        var additionalSeats = 10;
        var additionalServiceAccounts = 5;

        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup, additionalSeats, additionalServiceAccounts));
        Assert.Contains("Plan does not allow additional Service Accounts.", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenMoreSeatsThanPasswordManagerSeats(PlanType planType, SutProvider<SecretsManagerPlanValidation> sutProvider)
    {
        var plan = new Plan { BaseSeats = 10, HasAdditionalServiceAccountOption = true, Type = planType };
        var signup = new Organization { Seats = 5 };
        var additionalSeats = 10;
        var additionalServiceAccounts = 5;

        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup, additionalSeats, additionalServiceAccounts));
        Assert.Contains("You cannot have more Secrets Manager seats than Password Manager seats.", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenSubtractingServiceAccounts(PlanType planType, SutProvider<SecretsManagerPlanValidation> sutProvider)
    {
        var plan = new Plan { BaseSeats = 10, Type = planType };
        var signup = new Organization();
        var additionalSeats = 5;
        var additionalServiceAccounts = -5;

        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup, additionalSeats, additionalServiceAccounts));
        Assert.Contains("You can't subtract Service Accounts!", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenPlanDoesNotAllowAdditionalUsers(PlanType planType, SutProvider<SecretsManagerPlanValidation> sutProvider)
    {
        var plan = new Plan { HasAdditionalSeatsOption = false, Type = planType };
        var signup = new Organization();
        var additionalSeats = 10;
        var additionalServiceAccounts = 5;

        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup, additionalSeats, additionalServiceAccounts));
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenAdditionalSeatsExceedsMaxAllowed(PlanType planType, SutProvider<SecretsManagerPlanValidation> sutProvider)
    {
        var plan = new Plan { HasAdditionalSeatsOption = true, MaxAdditionalSeats = 5, Type = planType };
        var signup = new Organization();
        var additionalSeats = 10;
        var additionalServiceAccounts = 5;

        Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup, additionalSeats, additionalServiceAccounts));
    }
}
