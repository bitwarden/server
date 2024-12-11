using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Models.Business;

[SecretsManagerOrganizationCustomize]
public class SecretsManagerSubscriptionUpdateTests
{
    [Theory]
    [BitAutoData(PlanType.Custom)]
    [BitAutoData(PlanType.FamiliesAnnually)]
    [BitAutoData(PlanType.FamiliesAnnually2019)]
    public Task UpdateSubscriptionAsync_WithNonSecretsManagerPlanType_ThrowsBadRequestException(
        PlanType planType,
        Organization organization
    )
    {
        // Arrange
        organization.PlanType = planType;

        // Act
        var exception = Assert.Throws<NotFoundException>(
            () => new SecretsManagerSubscriptionUpdate(organization, false)
        );

        // Assert
        Assert.Contains(
            "Invalid Secrets Manager plan",
            exception.Message,
            StringComparison.InvariantCultureIgnoreCase
        );
        return Task.CompletedTask;
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseMonthly2019)]
    [BitAutoData(PlanType.EnterpriseMonthly2020)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually2019)]
    [BitAutoData(PlanType.EnterpriseAnnually2020)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.TeamsMonthly2019)]
    [BitAutoData(PlanType.TeamsMonthly2020)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually2019)]
    [BitAutoData(PlanType.TeamsAnnually2020)]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsStarter)]
    public void UpdateSubscription_WithNonSecretsManagerPlanType_DoesNotThrowException(
        PlanType planType,
        Organization organization
    )
    {
        // Arrange
        organization.PlanType = planType;

        // Act
        var ex = Record.Exception(() => new SecretsManagerSubscriptionUpdate(organization, false));

        // Assert
        Assert.Null(ex);
    }
}
