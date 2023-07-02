using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationAutoscaling;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationAutoscaling;
[SutProviderCustomize]
public class AutoscaleSecretsManagerSeatCommandTests
{
    [Theory]
    [BitAutoData(0, null, PlanType.EnterpriseAnnually)]
    [BitAutoData(5, null, PlanType.TeamsAnnually)]
    [BitAutoData(0, 10, PlanType.TeamsMonthly)]
    [BitAutoData(5, 10, PlanType.EnterpriseMonthly)]
    public void AutoscaleSeatsAsync_UpdatesMaxAutoscaleSmSeats(int? currentMaxAutoscaleSeats, int? newMaxAutoscaleSeats, PlanType planType,
        SutProvider<AutoscaleSecretsManagerSeatCommand> sutProvider)
    {
        // Arrange
        var organization = new Organization
        {
            SmSeats = 3,
            MaxAutoscaleSmSeats = currentMaxAutoscaleSeats,
            PlanType = planType,
        };
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == planType);

        plan.AllowSeatAutoscale = true;
        plan.MaxUsers = 10;

        var expectedMaxAutoscaleSeats = newMaxAutoscaleSeats;

        var result = sutProvider.Sut.AutoscaleSeatsAsync(organization, newMaxAutoscaleSeats);

        Assert.Equal(expectedMaxAutoscaleSeats, result.MaxAutoscaleSmSeats);
    }

    [Theory]
    [BitAutoData]
    public void AutoscaleSeatsAsync_ThrowsNotFoundException_WhenOrganizationIsNull(SutProvider<AutoscaleSecretsManagerSeatCommand> sutProvider)
    {
        Organization organization = null;
        int? maxAutoscaleSeats = 5;

        Assert.Throws<NotFoundException>(() => sutProvider.Sut.AutoscaleSeatsAsync(organization, maxAutoscaleSeats));
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void AutoscaleSeatsAsync_ThrowsBadRequestException_WhenMaxAutoscaleSeatsBelowCurrentSmSeats(PlanType planType, SutProvider<AutoscaleSecretsManagerSeatCommand> sutProvider)
    {
        var organization = new Organization
        {
            SmSeats = 5,
            PlanType = planType
        };
        int? maxAutoscaleSeats = 3;

        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.AutoscaleSeatsAsync(organization, maxAutoscaleSeats));
        Assert.Contains("Cannot set max Secrets Manager seat autoscaling below current Secrets Manager seat count.", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void AutoscaleSeatsAsync_ThrowsBadRequestException_WhenPlanDoesNotAllowSeatAutoscale(PlanType planType, SutProvider<AutoscaleSecretsManagerSeatCommand> sutProvider)
    {
        // Arrange
        var organization = new Organization
        {
            PlanType = planType,
        };
        int? maxAutoscaleSeats = 10;
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == planType);

        plan.AllowSeatAutoscale = false;

        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.AutoscaleSeatsAsync(organization, maxAutoscaleSeats));
        Assert.Contains("Your plan does not allow Secrets Manager seat autoscaling.", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void AutoscaleSeatsAsync_ThrowsBadRequestException_WhenMaxAutoscaleSeatsExceedPlanMaxUsers(PlanType planType, SutProvider<AutoscaleSecretsManagerSeatCommand> sutProvider)
    {
        var organization = new Organization
        {
            SmSeats = 3,
            PlanType = planType,
        };
        int? maxAutoscaleSeats = 15;
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == planType);

        plan.AllowSeatAutoscale = true;
        plan.MaxUsers = 10;

        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.AutoscaleSeatsAsync(organization, maxAutoscaleSeats));
        Assert.Contains("Your plan has a Secrets Manager seat limit of 10, but you have specified a max autoscale count of 15.Reduce your max autoscale seat count.", exception.Message);
    }
}
