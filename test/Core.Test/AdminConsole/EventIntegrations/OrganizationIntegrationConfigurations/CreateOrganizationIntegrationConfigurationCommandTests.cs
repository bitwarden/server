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
public class CreateOrganizationIntegrationConfigurationCommandTests
{
    [Theory, BitAutoData]
    public async Task CreateAsync_Success_CreatesConfigurationAndInvalidatesCache(
        SutProvider<CreateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration configuration)
    {
        integration.Id = integrationId;
        integration.OrganizationId = organizationId;
        integration.Type = IntegrationType.Webhook;
        configuration.OrganizationIntegrationId = integrationId;
        configuration.EventType = EventType.User_LoggedIn;

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .CreateAsync(configuration)
            .Returns(configuration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationValidator>()
            .ValidateConfiguration(Arg.Any<IntegrationType>(), Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(true);

        var result = await sutProvider.Sut.CreateAsync(organizationId, integrationId, configuration);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetByIdAsync(integrationId);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .CreateAsync(configuration);
        await sutProvider.GetDependency<IFusionCache>().Received(1)
            .RemoveAsync(EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
                organizationId,
                integration.Type,
                configuration.EventType.Value));
        // Also verify RemoveByTagAsync was NOT called
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
        Assert.Equal(configuration, result);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_WildcardSuccess_CreatesConfigurationAndInvalidatesCache(
        SutProvider<CreateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration configuration)
    {
        integration.Id = integrationId;
        integration.OrganizationId = organizationId;
        integration.Type = IntegrationType.Webhook;
        configuration.OrganizationIntegrationId = integrationId;
        configuration.EventType = null;

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .CreateAsync(configuration)
            .Returns(configuration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationValidator>()
            .ValidateConfiguration(Arg.Any<IntegrationType>(), Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(true);

        var result = await sutProvider.Sut.CreateAsync(organizationId, integrationId, configuration);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetByIdAsync(integrationId);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .CreateAsync(configuration);
        await sutProvider.GetDependency<IFusionCache>().Received(1)
            .RemoveByTagAsync(EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                organizationId,
                integration.Type));
        // Also verify RemoveAsync was NOT called
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
        Assert.Equal(configuration, result);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_IntegrationDoesNotExist_ThrowsNotFound(
        SutProvider<CreateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegrationConfiguration configuration)
    {
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns((OrganizationIntegration)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.CreateAsync(organizationId, integrationId, configuration));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .CreateAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_IntegrationDoesNotBelongToOrganization_ThrowsNotFound(
        SutProvider<CreateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration configuration)
    {
        integration.Id = integrationId;
        integration.OrganizationId = Guid.NewGuid(); // Different organization

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.CreateAsync(organizationId, integrationId, configuration));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .CreateAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_ValidationFails_ThrowsBadRequest(
        SutProvider<CreateOrganizationIntegrationConfigurationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration configuration)
    {
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationValidator>()
            .ValidateConfiguration(Arg.Any<IntegrationType>(), Arg.Any<OrganizationIntegrationConfiguration>())
            .Returns(false);

        integration.Id = integrationId;
        integration.OrganizationId = organizationId;
        configuration.OrganizationIntegrationId = integrationId;
        configuration.Template = "template";

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.CreateAsync(organizationId, integrationId, configuration));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().DidNotReceive()
            .CreateAsync(Arg.Any<OrganizationIntegrationConfiguration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
    }
}
