using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationIntegrations;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Test.AdminConsole.EventIntegrations.OrganizationIntegrations;

[SutProviderCustomize]
public class CreateOrganizationIntegrationCommandTests
{
    [Theory, BitAutoData]
    public async Task CreateAsync_Success_CreatesIntegrationAndInvalidatesCache(
        SutProvider<CreateOrganizationIntegrationCommand> sutProvider,
        OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Webhook;

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetManyByOrganizationAsync(integration.OrganizationId)
            .Returns([]);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .CreateAsync(integration)
            .Returns(integration);

        var result = await sutProvider.Sut.CreateAsync(integration);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetManyByOrganizationAsync(integration.OrganizationId);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .CreateAsync(integration);
        await sutProvider.GetDependency<IFusionCache>().Received(1)
            .RemoveByTagAsync(EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                integration.OrganizationId,
                integration.Type));
        Assert.Equal(integration, result);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_DuplicateType_ThrowsBadRequest(
        SutProvider<CreateOrganizationIntegrationCommand> sutProvider,
        OrganizationIntegration integration,
        OrganizationIntegration existingIntegration)
    {
        integration.Type = IntegrationType.Webhook;
        existingIntegration.Type = IntegrationType.Webhook;
        existingIntegration.OrganizationId = integration.OrganizationId;

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetManyByOrganizationAsync(integration.OrganizationId)
            .Returns([existingIntegration]);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.CreateAsync(integration));

        Assert.Contains("An integration of this type already exists", exception.Message);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceive()
            .CreateAsync(Arg.Any<OrganizationIntegration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_DifferentType_Success(
        SutProvider<CreateOrganizationIntegrationCommand> sutProvider,
        OrganizationIntegration integration,
        OrganizationIntegration existingIntegration)
    {
        integration.Type = IntegrationType.Webhook;
        existingIntegration.Type = IntegrationType.Slack;
        existingIntegration.OrganizationId = integration.OrganizationId;

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetManyByOrganizationAsync(integration.OrganizationId)
            .Returns([existingIntegration]);
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .CreateAsync(integration)
            .Returns(integration);

        var result = await sutProvider.Sut.CreateAsync(integration);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .CreateAsync(integration);
        Assert.Equal(integration, result);
    }
}
