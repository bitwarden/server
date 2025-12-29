using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrations;
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
public class DeleteOrganizationIntegrationCommandTests
{
    [Theory, BitAutoData]
    public async Task DeleteAsync_Success_DeletesIntegrationAndInvalidatesCache(
        SutProvider<DeleteOrganizationIntegrationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegration integration)
    {
        integration.Id = integrationId;
        integration.OrganizationId = organizationId;
        integration.Type = IntegrationType.Webhook;

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(integration);

        await sutProvider.Sut.DeleteAsync(organizationId, integrationId);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetByIdAsync(integrationId);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .DeleteAsync(integration);
        await sutProvider.GetDependency<IFusionCache>().Received(1)
            .RemoveByTagAsync(EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                organizationId,
                integration.Type));
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_IntegrationDoesNotExist_ThrowsNotFound(
        SutProvider<DeleteOrganizationIntegrationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId)
    {
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns((OrganizationIntegration)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(organizationId, integrationId));

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceive()
            .DeleteAsync(Arg.Any<OrganizationIntegration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_IntegrationDoesNotBelongToOrganization_ThrowsNotFound(
        SutProvider<DeleteOrganizationIntegrationCommand> sutProvider,
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
            () => sutProvider.Sut.DeleteAsync(organizationId, integrationId));

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceive()
            .DeleteAsync(Arg.Any<OrganizationIntegration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
    }
}
