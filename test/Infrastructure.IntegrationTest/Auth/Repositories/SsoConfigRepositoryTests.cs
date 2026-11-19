using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Auth.Repositories;

public class SsoConfigRepositoryTests
{
    [Theory, DatabaseData]
    public async Task GetManyByOrganizationIdsAsync_ReturnsOnlyRequestedOrganizations(
        ISsoConfigRepository ssoConfigRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var organization1 = await organizationRepository.CreateTestOrganizationAsync();
        var organization2 = await organizationRepository.CreateTestOrganizationAsync();
        var organizationWithoutSso = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();

        var ssoConfig1 = await ssoConfigRepository.CreateAsync(
            new SsoConfig { OrganizationId = organization1.Id, Enabled = true, Data = "{}" });
        var ssoConfig2 = await ssoConfigRepository.CreateAsync(
            new SsoConfig { OrganizationId = organization2.Id, Enabled = false, Data = "{}" });
        await ssoConfigRepository.CreateAsync(
            new SsoConfig { OrganizationId = otherOrganization.Id, Enabled = true, Data = "{}" });

        // Act
        var results = await ssoConfigRepository.GetManyByOrganizationIdsAsync(
            [organization1.Id, organization2.Id, organizationWithoutSso.Id]);

        // Assert
        Assert.Equal(
            new[] { ssoConfig1.Id, ssoConfig2.Id }.Order(),
            results.Select(sc => sc.Id).Order());
    }
}
