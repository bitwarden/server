using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Test.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations;

[SutProviderCustomize]
public class DeleteOrganizationIntegrationConfigurationCommandTests
{
    [Theory, BitAutoData]
    public async Task DeleteAsync_Success_DeletesConfigurationAndInvalidatesCache(
        SutProvider<DeleteOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration configuration)
    {
        integration.Id = integrationId;
        integration.OrganizationId = organizationId;
        integration.Type = IntegrationType.Webhook;
        configuration.Id = configurationId;
        configuration.OrganizationIntegrationId = integrationId;
        configuration.EventType = EventType.User_LoggedIn;

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(configurationId)
            .Returns(configuration);

        await sutProvider.Sut.DeleteAsync(organizationId, integrationId, configurationId);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetByIdAsync(integrationId);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .GetByIdAsync(configurationId);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .DeleteAsync(configuration);
        await sutProvider.GetDependency<IFusionCache>().Received(1)
            .RemoveAsync(EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
                organizationId,
                integration.Type,
                configuration.EventType.Value));
        // Also verify RemoveByTagAsync was NOT called
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_WildcardSuccess_DeletesConfigurationAndInvalidatesCache(
        SutProvider<DeleteOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration configuration)
    {
        integration.Id = integrationId;
        integration.OrganizationId = organizationId;
        integration.Type = IntegrationType.Webhook;
        configuration.Id = configurationId;
        configuration.OrganizationIntegrationId = integrationId;
        configuration.EventType = null;

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(configurationId)
            .Returns(configuration);

        await sutProvider.Sut.DeleteAsync(organizationId, integrationId, configurationId);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetByIdAsync(integrationId);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .GetByIdAsync(configurationId);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .DeleteAsync(configuration);
        await sutProvider.GetDependency<IFusionCache>().Received(1)
            .RemoveByTagAsync(EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                organizationId,
                integration.Type));
        // Also verify RemoveAsync was NOT called
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_IntegrationDoesNotExist_ThrowsNotFound(
        SutProvider<DeleteOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId)
    {
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns((OrganizationIntegration)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(organizationId, integrationId, configurationId));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .DeleteAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_IntegrationDoesNotBelongToOrganization_ThrowsNotFound(
        SutProvider<DeleteOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegration integration)
    {
        integration.Id = integrationId;
        integration.OrganizationId = Guid.NewGuid(); // Different organization

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(organizationId, integrationId, configurationId));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .DeleteAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_ConfigurationDoesNotExist_ThrowsNotFound(
        SutProvider<DeleteOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegration integration)
    {
        integration.Id = integrationId;
        integration.OrganizationId = organizationId;

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(configurationId)
            .Returns((OrganizationIntegrationConfiguration)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(organizationId, integrationId, configurationId));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .DeleteAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_ConfigurationDoesNotBelongToIntegration_ThrowsNotFound(
        SutProvider<DeleteOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration configuration)
    {
        integration.Id = integrationId;
        integration.OrganizationId = organizationId;
        configuration.Id = configurationId;
        configuration.OrganizationIntegrationId = Guid.NewGuid(); // Different integration

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(configurationId)
            .Returns(configuration);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(organizationId, integrationId, configurationId));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .DeleteAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
    }
}
