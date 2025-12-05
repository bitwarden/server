using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations;

[SutProviderCustomize]
public class GetOrganizationIntegrationConfigurationsQueryTests
{
    [Theory, BitAutoData]
    public async Task GetManyByIntegrationAsync_Success_ReturnsConfigurations(
        SutProvider<GetOrganizationIntegrationConfigurationsQuery> sutProvider,
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegration integration,
        List<OrganizationIntegrationConfiguration> configurations)
    {
        integration.Id = integrationId;
        integration.OrganizationId = organizationId;

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetManyByIntegrationAsync(integrationId)
            .Returns(configurations);

        var result = await sutProvider.Sut.GetManyByIntegrationAsync(organizationId, integrationId);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetByIdAsync(integrationId);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .GetManyByIntegrationAsync(integrationId);
        Assert.Equal(configurations.Count, result.Count);
    }

    [Theory, BitAutoData]
    public async Task GetManyByIntegrationAsync_NoConfigurations_ReturnsEmptyList(
        SutProvider<GetOrganizationIntegrationConfigurationsQuery> sutProvider,
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegration integration)
    {
        integration.Id = integrationId;
        integration.OrganizationId = organizationId;

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetManyByIntegrationAsync(integrationId)
            .Returns([]);

        var result = await sutProvider.Sut.GetManyByIntegrationAsync(organizationId, integrationId);

        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task GetManyByIntegrationAsync_IntegrationDoesNotExist_ThrowsNotFound(
        SutProvider<GetOrganizationIntegrationConfigurationsQuery> sutProvider,
        Guid organizationId,
        Guid integrationId)
    {
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns((OrganizationIntegration)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.GetManyByIntegrationAsync(organizationId, integrationId));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .GetManyByIntegrationAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetManyByIntegrationAsync_IntegrationDoesNotBelongToOrganization_ThrowsNotFound(
        SutProvider<GetOrganizationIntegrationConfigurationsQuery> sutProvider,
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegration integration)
    {
        integration.Id = integrationId;
        integration.OrganizationId = Guid.NewGuid(); // Different organization

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.GetManyByIntegrationAsync(organizationId, integrationId));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .GetManyByIntegrationAsync(Arg.Any<Guid>());
    }
}
