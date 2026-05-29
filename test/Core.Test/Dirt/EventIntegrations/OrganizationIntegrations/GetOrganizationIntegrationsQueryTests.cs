using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.EventIntegrations.OrganizationIntegrations;
using Bit.Core.Dirt.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.EventIntegrations.OrganizationIntegrations;

[SutProviderCustomize]
public class GetOrganizationIntegrationsQueryTests
{
    [Theory, BitAutoData]
    public async Task GetManyByOrganizationAsync_CallsRepository(
        SutProvider<GetOrganizationIntegrationsQuery> sutProvider,
        Guid organizationId,
        List<OrganizationIntegration> integrations)
    {
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetManyByOrganizationAsync(organizationId)
            .Returns(integrations);

        var result = await sutProvider.Sut.GetManyByOrganizationAsync(organizationId);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetManyByOrganizationAsync(organizationId);
        Assert.Equal(integrations.Count, result.Count);
    }

    [Theory, BitAutoData]
    public async Task GetManyByOrganizationAsync_NoIntegrations_ReturnsEmptyList(
        SutProvider<GetOrganizationIntegrationsQuery> sutProvider,
        Guid organizationId)
    {
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetManyByOrganizationAsync(organizationId)
            .Returns([]);

        var result = await sutProvider.Sut.GetManyByOrganizationAsync(organizationId);

        Assert.Empty(result);
    }
}
