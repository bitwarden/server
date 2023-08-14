using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;
namespace Bit.Core.Test.Models.Business;

[SutProviderCustomize]
[SecretsManagerOrganizationCustomize]
public class SecretsManagerSubscriptionUpdateTests
{
    [Theory]
    [BitAutoData(PlanType.Custom)]
    [BitAutoData(PlanType.FamiliesAnnually)]
    [BitAutoData(PlanType.FamiliesAnnually2019)]
    [BitAutoData(PlanType.EnterpriseMonthly2019)]
    [BitAutoData(PlanType.EnterpriseAnnually2019)]
    [BitAutoData(PlanType.TeamsMonthly2019)]
    [BitAutoData(PlanType.TeamsAnnually2019)]
    public async Task UpdateSubscriptionAsync_WithNonSecretsManagerPlanType_ThrowsBadRequestException(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = planType;

        var exception = Assert.Throws<NotFoundException>(() => new SecretsManagerSubscriptionUpdate(organization, false));
        Assert.Contains("Invalid Secrets Manager plan", exception.Message, StringComparison.InvariantCultureIgnoreCase);
    }
}
