using System.Text.Json;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.EventIntegrations.OrganizationIntegrationConfigurations;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Models.Data.Teams;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Dirt.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Test.Dirt.EventIntegrations.OrganizationIntegrationConfigurations;

[SutProviderCustomize]
public class UpdateOrganizationIntegrationConfigurationCommandTests
{
    private const string _teamId = "19:team@thread.tacv2";
    private const string _standardChannelId = "19:alerts@thread.tacv2";
    private const string _privateChannelId = "19:private@thread.tacv2";
    private static readonly Uri _serviceUrl = new("https://smba.example.com/amer/tenant/");

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
                existingConfiguration.EventType.Value));
        // Also verify RemoveByTagAsync was NOT called
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
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
            .RemoveByTagAsync(EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                organizationId,
                integration.Type));
        // Also verify RemoveAsync was NOT called
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
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
        updatedConfiguration.EventType = EventType.Cipher_Created;

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
                existingConfiguration.EventType.Value));
        await sutProvider.GetDependency<IFusionCache>().Received(1)
            .RemoveAsync(EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
                organizationId,
                integration.Type,
                updatedConfiguration.EventType.Value));
        // Verify RemoveByTagAsync was NOT called since both are specific event types
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
        Assert.Equal(updatedConfiguration, result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_IntegrationDoesNotExist_ThrowsBadRequest(
        SutProvider<UpdateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegrationConfiguration updatedConfiguration)
    {
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns((OrganizationIntegration)null);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, updatedConfiguration));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .ReplaceAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_IntegrationDoesNotBelongToOrganization_ThrowsBadRequest(
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

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, updatedConfiguration));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .ReplaceAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ConfigurationDoesNotExist_ThrowsBadRequest(
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

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, updatedConfiguration));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .ReplaceAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ConfigurationDoesNotBelongToIntegration_ThrowsBadRequest(
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

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, updatedConfiguration));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .ReplaceAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
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
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ChangedFromWildcardToSpecific_InvalidatesAllCaches(
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration existingConfiguration,
        OrganizationIntegrationConfiguration updatedConfiguration,
        SutProvider<UpdateOrganizationIntegrationConfigurationCommand> sutProvider)
    {
        integration.OrganizationId = organizationId;
        integration.Type = IntegrationType.Webhook;
        existingConfiguration.OrganizationIntegrationId = integrationId;
        existingConfiguration.EventType = null; // Wildcard
        updatedConfiguration.EventType = EventType.User_LoggedIn; // Specific

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId).Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(existingConfiguration.Id).Returns(existingConfiguration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationValidator>()
            .ValidateConfiguration(Arg.Any<IntegrationType>(), Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(true);

        await sutProvider.Sut.UpdateAsync(organizationId, integrationId, existingConfiguration.Id, updatedConfiguration);

        await sutProvider.GetDependency<IFusionCache>().Received(1)
            .RemoveByTagAsync(EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                organizationId,
                integration.Type));
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ChangedFromSpecificToWildcard_InvalidatesAllCaches(
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration existingConfiguration,
        OrganizationIntegrationConfiguration updatedConfiguration,
        SutProvider<UpdateOrganizationIntegrationConfigurationCommand> sutProvider)
    {
        integration.OrganizationId = organizationId;
        integration.Type = IntegrationType.Webhook;
        existingConfiguration.OrganizationIntegrationId = integrationId;
        existingConfiguration.EventType = EventType.User_LoggedIn; // Specific
        updatedConfiguration.EventType = null; // Wildcard

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId).Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .GetByIdAsync(existingConfiguration.Id).Returns(existingConfiguration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationValidator>()
            .ValidateConfiguration(Arg.Any<IntegrationType>(), Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(true);

        await sutProvider.Sut.UpdateAsync(organizationId, integrationId, existingConfiguration.Id, updatedConfiguration);

        await sutProvider.GetDependency<IFusionCache>().Received(1)
            .RemoveByTagAsync(EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                organizationId,
                integration.Type));
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_TeamsStandardChannel_Updates(
        SutProvider<UpdateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration existingConfiguration,
        OrganizationIntegrationConfiguration updatedConfiguration)
    {
        SetupTeamsIntegration(integration, organizationId, integrationId);
        SetupStandardChannels(sutProvider);
        existingConfiguration.Id = configurationId;
        existingConfiguration.OrganizationIntegrationId = integrationId;
        existingConfiguration.Configuration = null;
        updatedConfiguration.Configuration = JsonSerializer.Serialize(new TeamsIntegrationConfiguration(_standardChannelId));
        sutProvider.GetDependency<IOrganizationIntegrationRepository>().GetByIdAsync(integrationId).Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().GetByIdAsync(configurationId).Returns(existingConfiguration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationValidator>()
            .ValidateConfiguration(Arg.Any<IntegrationType>(), Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(true);

        await sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, updatedConfiguration);

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .ReplaceAsync(updatedConfiguration);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_TeamsPrivateChannel_ThrowsBadRequest(
        SutProvider<UpdateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration existingConfiguration,
        OrganizationIntegrationConfiguration updatedConfiguration)
    {
        SetupTeamsIntegration(integration, organizationId, integrationId);
        SetupStandardChannels(sutProvider);
        existingConfiguration.Id = configurationId;
        existingConfiguration.OrganizationIntegrationId = integrationId;
        existingConfiguration.Configuration = null;
        updatedConfiguration.Configuration = JsonSerializer.Serialize(new TeamsIntegrationConfiguration(_privateChannelId));
        sutProvider.GetDependency<IOrganizationIntegrationRepository>().GetByIdAsync(integrationId).Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().GetByIdAsync(configurationId).Returns(existingConfiguration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationValidator>()
            .ValidateConfiguration(Arg.Any<IntegrationType>(), Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, updatedConfiguration));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_TeamsChannelLookupFails_ThrowsBadRequest(
        SutProvider<UpdateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration existingConfiguration,
        OrganizationIntegrationConfiguration updatedConfiguration)
    {
        SetupTeamsIntegration(integration, organizationId, integrationId);
        existingConfiguration.Id = configurationId;
        existingConfiguration.OrganizationIntegrationId = integrationId;
        existingConfiguration.Configuration = null;
        updatedConfiguration.Configuration = JsonSerializer.Serialize(new TeamsIntegrationConfiguration(_standardChannelId));
        sutProvider.GetDependency<IOrganizationIntegrationRepository>().GetByIdAsync(integrationId).Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().GetByIdAsync(configurationId).Returns(existingConfiguration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationValidator>()
            .ValidateConfiguration(Arg.Any<IntegrationType>(), Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(true);
        sutProvider.GetDependency<ITeamsService>()
            .GetStandardChannelsAsync(_serviceUrl, _teamId)
            .Returns((IReadOnlyList<TeamsChannel>?)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, updatedConfiguration));

        Assert.Contains("Unable to retrieve the channels", exception.Message);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_TeamsChannelUnchanged_SkipsChannelLookup(
        SutProvider<UpdateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration existingConfiguration,
        OrganizationIntegrationConfiguration updatedConfiguration)
    {
        SetupTeamsIntegration(integration, organizationId, integrationId);
        existingConfiguration.Id = configurationId;
        existingConfiguration.OrganizationIntegrationId = integrationId;
        existingConfiguration.Configuration = JsonSerializer.Serialize(new TeamsIntegrationConfiguration(_standardChannelId));
        updatedConfiguration.Configuration = JsonSerializer.Serialize(new TeamsIntegrationConfiguration(_standardChannelId));
        sutProvider.GetDependency<IOrganizationIntegrationRepository>().GetByIdAsync(integrationId).Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().GetByIdAsync(configurationId).Returns(existingConfiguration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationValidator>()
            .ValidateConfiguration(Arg.Any<IntegrationType>(), Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(true);

        await sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, updatedConfiguration);

        await sutProvider.GetDependency<ITeamsService>().DidNotReceiveWithAnyArgs()
            .GetStandardChannelsAsync(default!, default!);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .ReplaceAsync(updatedConfiguration);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_TeamsChannelChanged_ValidatesNewChannel(
        SutProvider<UpdateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration existingConfiguration,
        OrganizationIntegrationConfiguration updatedConfiguration)
    {
        SetupTeamsIntegration(integration, organizationId, integrationId);
        SetupStandardChannels(sutProvider);
        existingConfiguration.Id = configurationId;
        existingConfiguration.OrganizationIntegrationId = integrationId;
        existingConfiguration.Configuration = JsonSerializer.Serialize(new TeamsIntegrationConfiguration(_standardChannelId));
        updatedConfiguration.Configuration = JsonSerializer.Serialize(new TeamsIntegrationConfiguration(_privateChannelId));
        sutProvider.GetDependency<IOrganizationIntegrationRepository>().GetByIdAsync(integrationId).Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().GetByIdAsync(configurationId).Returns(existingConfiguration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationValidator>()
            .ValidateConfiguration(Arg.Any<IntegrationType>(), Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, integrationId, configurationId, updatedConfiguration));

        await sutProvider.GetDependency<ITeamsService>().Received(1)
            .GetStandardChannelsAsync(_serviceUrl, _teamId);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default!);
    }

    private static void SetupTeamsIntegration(OrganizationIntegration integration, Guid organizationId, Guid integrationId)
    {
        integration.Id = integrationId;
        integration.OrganizationId = organizationId;
        integration.Type = IntegrationType.Teams;
        integration.Configuration = JsonSerializer.Serialize(new TeamsIntegration(
            TenantId: "tenant",
            Teams: [],
            ChannelId: _teamId,
            ServiceUrl: _serviceUrl));
    }

    private static void SetupStandardChannels<T>(SutProvider<T> sutProvider)
    {
        sutProvider.GetDependency<ITeamsService>()
            .GetStandardChannelsAsync(_serviceUrl, _teamId)
            .Returns([
                new TeamsChannel { Id = _teamId, Type = "standard" },
                new TeamsChannel { Id = _standardChannelId, Name = "Alerts", Type = "standard" }
            ]);
    }
}
