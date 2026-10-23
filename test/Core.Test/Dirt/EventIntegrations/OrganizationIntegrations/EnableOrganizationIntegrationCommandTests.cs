using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.EventIntegrations.OrganizationIntegrations;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Test.Dirt.EventIntegrations.OrganizationIntegrations;

[SutProviderCustomize]
public class EnableOrganizationIntegrationCommandTests
{
    [Theory, BitAutoData]
    public async Task EnableAsync_DisabledConfigurations_ReEnablesThemAndInvalidatesCache(
        SutProvider<EnableOrganizationIntegrationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegration integration)
    {
        integration.Id = integrationId;
        integration.OrganizationId = organizationId;
        integration.Type = IntegrationType.Slack;

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .ClearDisabledByIntegrationAsync(organizationId, integrationId, Arg.Any<DateTime>())
            .Returns(3);

        var reEnabled = await sutProvider.Sut.EnableAsync(organizationId, integrationId);

        Assert.Equal(3, reEnabled);
        await sutProvider.GetDependency<IFusionCache>().Received(1)
            .RemoveByTagAsync(EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                organizationId,
                IntegrationType.Slack));
    }

    [Theory, BitAutoData]
    public async Task EnableAsync_NothingDisabled_SkipsCacheInvalidation(
        SutProvider<EnableOrganizationIntegrationCommand> sutProvider,
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
            .ClearDisabledByIntegrationAsync(organizationId, integrationId, Arg.Any<DateTime>())
            .Returns(0);

        var reEnabled = await sutProvider.Sut.EnableAsync(organizationId, integrationId);

        Assert.Equal(0, reEnabled);
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(
                Arg.Any<string>(),
                Arg.Any<FusionCacheEntryOptions>(),
                Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task EnableAsync_IntegrationDoesNotExist_ThrowsBadRequest(
        SutProvider<EnableOrganizationIntegrationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId)
    {
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns((OrganizationIntegration)null);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.EnableAsync(organizationId, integrationId));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .DidNotReceiveWithAnyArgs()
            .ClearDisabledByIntegrationAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task EnableAsync_IntegrationBelongsToAnotherOrganization_ThrowsBadRequest(
        SutProvider<EnableOrganizationIntegrationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegration integration)
    {
        integration.Id = integrationId;
        integration.OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.EnableAsync(organizationId, integrationId));

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .DidNotReceiveWithAnyArgs()
            .ClearDisabledByIntegrationAsync(default, default, default);
    }
}
