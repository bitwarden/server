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
    [BitAutoData(2, 5, 2, 0)]
    [BitAutoData(0, 5, 2, 0)]
    [BitAutoData(6, 5, 2, 3)]
    [BitAutoData(2, 5, 10, 7)]
    public async Task CountNewServiceAccountSlotsRequiredAsync_ReturnsCorrectCount(
        int serviceAccountsToAdd,
        int organizationSmServiceAccounts,
        int currentServiceAccounts,
        int expectedNewServiceAccountsRequired,
        Organization organization,
        SutProvider<CountNewServiceAccountSlotsRequiredQuery> sutProvider)
    {
        organization.UseSecretsManager = true;
        organization.SmServiceAccounts = organizationSmServiceAccounts;
        organization.SecretsManagerBeta = false;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetServiceAccountCountByOrganizationIdAsync(organization.Id)
            .Returns(currentServiceAccounts);

        var result = await sutProvider.Sut.CountNewServiceAccountSlotsRequiredAsync(organization.Id, serviceAccountsToAdd);

        Assert.Equal(expectedNewServiceAccountsRequired, result);

        if (serviceAccountsToAdd > 0)
        {
            await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
                .GetServiceAccountCountByOrganizationIdAsync(organization.Id);
        }
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

        organization.UseSecretsManager = true;
        organization.SmServiceAccounts = null;
        organization.SecretsManagerBeta = false;

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
    public async Task CountNewServiceAccountSlotsRequiredAsync_WithSecretsManagerBeta_ReturnsZero(
        int serviceAccountsToAdd,
        Organization organization,
        SutProvider<CountNewServiceAccountSlotsRequiredQuery> sutProvider)
    {
        organization.UseSecretsManager = true;
        organization.SecretsManagerBeta = true;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var result = await sutProvider.Sut.CountNewServiceAccountSlotsRequiredAsync(organization.Id, serviceAccountsToAdd);

        Assert.Equal(0, result);

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

    [Theory, BitAutoData]
    public async Task CountNewServiceAccountSlotsRequiredAsync_WithOrganizationUseSecretsManagerFalse_ThrowsNotFound(
        Organization organization, int serviceAccountsToAdd,
        SutProvider<CountNewServiceAccountSlotsRequiredQuery> sutProvider)
    {
        organization.UseSecretsManager = false;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CountNewServiceAccountSlotsRequiredAsync(organization.Id, serviceAccountsToAdd));
    }
}
