using Bit.Commercial.Core.SecretsManager.Queries.ServiceAccounts;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Queries.ServiceAccounts;

[SutProviderCustomize]
public class CountNewServiceAccountSlotsRequiredQueryTests
{
    [Theory]
    [BitAutoData(5, 2)]
    [BitAutoData(5, 0)]
    [BitAutoData(5, 6)]
    public async Task CountNewServiceAccountSlotsRequiredAsync_ReturnsCorrectCount(
        int organizationSmServiceAccounts,
        int currentServiceAccounts,
        int serviceAccountsToAdd,
        Organization organization,
        SutProvider<CountNewServiceAccountSlotsRequiredQuery> sutProvider)
    {
        var availableServiceAccountSlots = Math.Max(0, organizationSmServiceAccounts - currentServiceAccounts);
        var expectedAvailableServiceAccounts = Math.Max(0, serviceAccountsToAdd - availableServiceAccountSlots);

        organization.SmServiceAccounts = organizationSmServiceAccounts;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetServiceAccountCountByOrganizationIdAsync(organization.Id)
            .Returns(currentServiceAccounts);

        var result = await sutProvider.Sut.CountNewServiceAccountSlotsRequiredAsync(organization.Id, serviceAccountsToAdd);

        Assert.Equal(expectedAvailableServiceAccounts, result);

        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
            .GetServiceAccountCountByOrganizationIdAsync(organization.Id);
    }

    [Theory]
    [BitAutoData(0)]
    [BitAutoData(5)]
    public async Task CountNewServiceAccountSlotsRequiredAsync_WithNullSmServiceAccounts_ReturnsZero(
        int currentServiceAccounts,
        int serviceAccountsToAdd,
        Organization organization,
        SutProvider<CountNewServiceAccountSlotsRequiredQuery> sutProvider)
    {
        const int expectedRequiredServiceAccountsToScale = 0;

        organization.SmServiceAccounts = null;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetServiceAccountCountByOrganizationIdAsync(organization.Id)
            .Returns(currentServiceAccounts);

        var result = await sutProvider.Sut.CountNewServiceAccountSlotsRequiredAsync(organization.Id, serviceAccountsToAdd);

        Assert.Equal(expectedRequiredServiceAccountsToScale, result);

        await sutProvider.GetDependency<IServiceAccountRepository>().DidNotReceiveWithAnyArgs()
            .GetServiceAccountCountByOrganizationIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task CountNewServiceAccountSlotsRequiredAsync_WithNonExistentOrganizationId_ThrowsNotFound(
        Guid organizationId, int serviceAccountsToAdd,
        SutProvider<CountNewServiceAccountSlotsRequiredQuery> sutProvider)
    {
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CountNewServiceAccountSlotsRequiredAsync(organizationId, serviceAccountsToAdd));
    }
}
