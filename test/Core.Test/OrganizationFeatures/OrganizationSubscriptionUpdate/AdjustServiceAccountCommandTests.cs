using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSubscriptionUpdate;

[SutProviderCustomize]
public class AdjustServiceAccountCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task AdjustServiceAccountAsync_NoServiceAccountLimit_ThrowsBadRequestException(
        Organization organization,
        int serviceAccountAdjustment,
        SutProvider<AdjustServiceAccountsCommand> sutProvider)
    {
        organization.SmServiceAccounts = null;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdjustServiceAccountsAsync(organization, serviceAccountAdjustment));
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustServiceAccountAsync_NoPaymentMethod_ThrowsBadRequestException(
        Organization organization,
        int serviceAccountAdjustment,
        SutProvider<AdjustServiceAccountsCommand> sutProvider)
    {
        organization.GatewayCustomerId = null;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdjustServiceAccountsAsync(organization, serviceAccountAdjustment));
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustServiceAccountAsync_NoSubscription_ThrowsBadRequestException(
        Organization organization,
        int serviceAccountAdjustment,
        SutProvider<AdjustServiceAccountsCommand> sutProvider)
    {
        organization.GatewaySubscriptionId = null;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdjustServiceAccountsAsync(organization, serviceAccountAdjustment));
    }


}
