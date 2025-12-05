using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Repositories;
using Bit.Core.Test.Billing.Mocks.Plans;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class GetOrganizationSubscriptionsToUpdateQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetOrganizationSubscriptionsToUpdateAsync_WhenNoOrganizationsNeedToBeSynced_ThenAnEmptyListIsReturned(
        SutProvider<GetOrganizationSubscriptionsToUpdateQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOrganizationsForSubscriptionSyncAsync()
            .Returns([]);

        var result = await sutProvider.Sut.GetOrganizationSubscriptionsToUpdateAsync();

        Assert.Empty(result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationSubscriptionsToUpdateAsync_WhenOrganizationsNeedToBeSynced_ThenUpdateIsReturnedWithCorrectPlanAndOrg(
        Organization organization,
        SutProvider<GetOrganizationSubscriptionsToUpdateQuery> sutProvider)
    {
        organization.PlanType = PlanType.EnterpriseAnnually2023;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOrganizationsForSubscriptionSyncAsync()
            .Returns([organization]);

        sutProvider.GetDependency<IPricingClient>()
            .ListPlans()
            .Returns([new Enterprise2023Plan(true)]);

        var result = await sutProvider.Sut.GetOrganizationSubscriptionsToUpdateAsync();

        var matchingUpdate = result.FirstOrDefault(x => x.Organization.Id == organization.Id);
        Assert.NotNull(matchingUpdate);
        Assert.Equal(organization.PlanType, matchingUpdate.Plan!.Type);
        Assert.Equal(organization, matchingUpdate.Organization);
    }
}
