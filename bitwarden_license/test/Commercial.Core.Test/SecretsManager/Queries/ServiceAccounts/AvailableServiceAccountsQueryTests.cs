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
public class AvailableServiceAccountsQueryTests
{
    [Theory]
    [BitAutoData(5, 2)]
    [BitAutoData(5, 0)]
    [BitAutoData(5, 6)]
    public async Task GetAvailableServiceAccountsAsync_ReturnsCorrectCount(
        int smServiceAccounts,
        int currentServiceAccounts,
        Organization organization,
        SutProvider<AvailableServiceAccountsQuery> sutProvider)
    {
        var expectedAvailableServiceAccounts = Math.Max(0, smServiceAccounts - currentServiceAccounts);

        organization.SmServiceAccounts = smServiceAccounts;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetServiceAccountCountByOrganizationIdAsync(organization.Id)
            .Returns(currentServiceAccounts);

        var result = await sutProvider.Sut.GetAvailableServiceAccountsAsync(organization.Id);

        Assert.Equal(expectedAvailableServiceAccounts, result);

        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
            .GetServiceAccountCountByOrganizationIdAsync(organization.Id);
    }

    [Theory]
    [BitAutoData(0)]
    [BitAutoData(5)]
    public async Task GetAvailableServiceAccountsAsync_WithNullSmServiceAccounts_ReturnsZero(
        int currentServiceAccounts,
        Organization organization,
        SutProvider<AvailableServiceAccountsQuery> sutProvider)
    {
        var expectedAvailableServiceAccounts = 0;

        organization.SmServiceAccounts = null;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetServiceAccountCountByOrganizationIdAsync(organization.Id)
            .Returns(currentServiceAccounts);

        var result = await sutProvider.Sut.GetAvailableServiceAccountsAsync(organization.Id);

        Assert.Equal(expectedAvailableServiceAccounts, result);

        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
            .GetServiceAccountCountByOrganizationIdAsync(organization.Id);
    }

    [Theory, BitAutoData]
    public async Task GetAvailableServiceAccountsAsync_WithNonExistentOrganizationId_ThrowsNotFound(
        Guid organizationId,
        SutProvider<AvailableServiceAccountsQuery> sutProvider)
    {
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.GetAvailableServiceAccountsAsync(organizationId));
    }
}
