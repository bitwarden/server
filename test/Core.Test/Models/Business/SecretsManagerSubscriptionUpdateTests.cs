using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Test.Billing.Mocks;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Models.Business;

[SecretsManagerOrganizationCustomize]
public class SecretsManagerSubscriptionUpdateTests
{
    private static TheoryData<Plan> ToPlanTheory(List<PlanType> types)
    {
        var theoryData = new TheoryData<Plan>();
        var plans = types.Select(MockPlans.Get).ToArray();
        theoryData.AddRange(plans);
        return theoryData;
    }

    public static TheoryData<Plan> NonSmPlans =>
        ToPlanTheory([PlanType.Custom, PlanType.FamiliesAnnually, PlanType.FamiliesAnnually2025, PlanType.FamiliesAnnually2019]);

    public static TheoryData<Plan> SmPlans => ToPlanTheory([
        PlanType.EnterpriseAnnually2019,
        PlanType.EnterpriseAnnually,
        PlanType.TeamsMonthly2019,
        PlanType.TeamsAnnually2020,
        PlanType.TeamsMonthly,
        PlanType.TeamsAnnually2019,
        PlanType.TeamsAnnually2020,
        PlanType.TeamsAnnually,
        PlanType.TeamsStarter
    ]);

    [Theory]
    [BitMemberAutoData(nameof(NonSmPlans))]
    public Task UpdateSubscriptionAsync_WithNonSecretsManagerPlanType_ThrowsBadRequestException(
        Plan plan,
        Organization organization)
    {
        // Arrange
        organization.PlanType = plan.Type;

        // Act
        var exception = Assert.Throws<NotFoundException>(() => new SecretsManagerSubscriptionUpdate(organization, plan, false));

        // Assert
        Assert.Contains("Invalid Secrets Manager plan", exception.Message, StringComparison.InvariantCultureIgnoreCase);
        return Task.CompletedTask;
    }

    [Theory]
    [BitMemberAutoData(nameof(SmPlans))]
    public void UpdateSubscription_WithNonSecretsManagerPlanType_DoesNotThrowException(
        Plan plan,
        Organization organization)
    {
        // Arrange
        organization.PlanType = plan.Type;

        // Act
        var ex = Record.Exception(() => new SecretsManagerSubscriptionUpdate(organization, plan, false));

        // Assert
        Assert.Null(ex);
    }
}
