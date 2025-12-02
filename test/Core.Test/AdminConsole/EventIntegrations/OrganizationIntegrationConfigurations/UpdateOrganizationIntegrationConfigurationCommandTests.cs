using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations;
using Bit.Core.AdminConsole.Services;
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
public class UpdateOrganizationIntegrationConfigurationCommandTests
{
    [Theory, BitAutoData]
    public async Task UpdateAsync_Success_UpdatesConfigurationAndInvalidatesCache(
        SutProvider<UpdateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration existingConfiguration,
        OrganizationIntegrationConfiguration updatedConfiguration)
    {
        integration.Id = integrationId;
        integration.OrganizationId = organizationId;
        integration.Type = IntegrationType.Webhook;
        existingConfiguration.Id = configurationId;
        existingConfiguration.OrganizationIntegrationId = integrationId;
        existingConfiguration.EventType = EventType.User_LoggedIn;
        updatedConfiguration.Id = configurationId;
        updatedConfiguration.OrganizationIntegrationId = integrationId;
        existingConfiguration.EventType = EventType.User_LoggedIn;

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(configurationId)
            .Returns(existingConfiguration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationValidator>()
            .ValidateConfiguration(Arg.Any<IntegrationType>(), Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(true);

        var result = await sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, updatedConfiguration);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetByIdAsync(integrationId);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .GetByIdAsync(configurationId);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .ReplaceAsync(updatedConfiguration);
        await sutProvider.GetDependency<IFusionCache>().Received(1)
            .RemoveAsync(EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
                organizationId,
                integration.Type,
                existingConfiguration.EventType));
        Assert.Equal(updatedConfiguration, result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WildcardSuccess_UpdatesConfigurationAndInvalidatesCache(
        SutProvider<UpdateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration existingConfiguration,
        OrganizationIntegrationConfiguration updatedConfiguration)
    {
        integration.Id = integrationId;
        integration.OrganizationId = organizationId;
        integration.Type = IntegrationType.Webhook;
        existingConfiguration.Id = configurationId;
        existingConfiguration.OrganizationIntegrationId = integrationId;
        existingConfiguration.EventType = null;
        updatedConfiguration.Id = configurationId;
        updatedConfiguration.OrganizationIntegrationId = integrationId;
        updatedConfiguration.EventType = null;

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(configurationId)
            .Returns(existingConfiguration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationValidator>()
            .ValidateConfiguration(Arg.Any<IntegrationType>(), Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(true);

        var result = await sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, updatedConfiguration);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetByIdAsync(integrationId);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .GetByIdAsync(configurationId);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .ReplaceAsync(updatedConfiguration);
        await sutProvider.GetDependency<IFusionCache>().Received(1)
            .RemoveAsync(EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
                organizationId,
                integration.Type,
                null));
        Assert.Equal(updatedConfiguration, result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ChangedEventType_UpdatesConfigurationAndInvalidatesCacheForBothTypes(
        SutProvider<UpdateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration existingConfiguration,
        OrganizationIntegrationConfiguration updatedConfiguration)
    {
        integration.Id = integrationId;
        integration.OrganizationId = organizationId;
        integration.Type = IntegrationType.Webhook;
        existingConfiguration.Id = configurationId;
        existingConfiguration.OrganizationIntegrationId = integrationId;
        existingConfiguration.EventType = EventType.User_LoggedIn;
        updatedConfiguration.Id = configurationId;
        updatedConfiguration.OrganizationIntegrationId = integrationId;
        existingConfiguration.EventType = EventType.Cipher_Created;

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(configurationId)
            .Returns(existingConfiguration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationValidator>()
            .ValidateConfiguration(Arg.Any<IntegrationType>(), Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(true);

        var result = await sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, updatedConfiguration);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetByIdAsync(integrationId);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .GetByIdAsync(configurationId);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .ReplaceAsync(updatedConfiguration);
        await sutProvider.GetDependency<IFusionCache>().Received(1)
            .RemoveAsync(EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
                organizationId,
                integration.Type,
                existingConfiguration.EventType));
        await sutProvider.GetDependency<IFusionCache>().Received(1)
            .RemoveAsync(EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
                organizationId,
                integration.Type,
                updatedConfiguration.EventType));
        Assert.Equal(updatedConfiguration, result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_IntegrationDoesNotExist_ThrowsNotFound(
        SutProvider<UpdateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegrationConfiguration updatedConfiguration)
    {
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns((OrganizationIntegration)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, updatedConfiguration));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .ReplaceAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_IntegrationDoesNotBelongToOrganization_ThrowsNotFound(
        SutProvider<UpdateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration updatedConfiguration)
    {
        integration.Id = integrationId;
        integration.OrganizationId = Guid.NewGuid(); // Different organization

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, updatedConfiguration));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .ReplaceAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ConfigurationDoesNotExist_ThrowsNotFound(
        SutProvider<UpdateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration updatedConfiguration)
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
            () => sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, updatedConfiguration));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .ReplaceAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ConfigurationDoesNotBelongToIntegration_ThrowsNotFound(
        SutProvider<UpdateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration existingConfiguration,
        OrganizationIntegrationConfiguration updatedConfiguration)
    {
        integration.Id = integrationId;
        integration.OrganizationId = organizationId;
        existingConfiguration.Id = configurationId;
        existingConfiguration.OrganizationIntegrationId = Guid.NewGuid(); // Different integration

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(configurationId)
            .Returns(existingConfiguration);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, updatedConfiguration));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .ReplaceAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ValidationFails_ThrowsBadRequest(
        SutProvider<UpdateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration existingConfiguration,
        OrganizationIntegrationConfiguration updatedConfiguration)
    {
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationValidator>()
            .ValidateConfiguration(Arg.Any<IntegrationType>(), Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(false);

        integration.Id = integrationId;
        integration.OrganizationId = organizationId;
        existingConfiguration.Id = configurationId;
        existingConfiguration.OrganizationIntegrationId = integrationId;
        updatedConfiguration.Id = configurationId;
        updatedConfiguration.OrganizationIntegrationId = integrationId;
        updatedConfiguration.Template = "template";

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(configurationId)
            .Returns(existingConfiguration);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, updatedConfiguration));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .ReplaceAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
    }
}
