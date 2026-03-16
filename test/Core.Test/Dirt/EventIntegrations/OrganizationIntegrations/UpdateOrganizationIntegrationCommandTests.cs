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
public class UpdateOrganizationIntegrationCommandTests
{
    [Theory, BitAutoData]
    public async Task UpdateAsync_Success_UpdatesIntegrationAndInvalidatesCache(
        SutProvider<UpdateOrganizationIntegrationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegration existingIntegration,
        OrganizationIntegration updatedIntegration)
    {
        existingIntegration.Id = integrationId;
        existingIntegration.OrganizationId = organizationId;
        existingIntegration.Type = IntegrationType.Webhook;
        updatedIntegration.Id = integrationId;
        updatedIntegration.OrganizationId = organizationId;
        updatedIntegration.Type = IntegrationType.Webhook;

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(existingIntegration);

        var result = await sutProvider.Sut.UpdateAsync(organizationId, integrationId, updatedIntegration);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetByIdAsync(integrationId);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .ReplaceAsync(updatedIntegration);
        await sutProvider.GetDependency<IFusionCache>().Received(1)
            .RemoveByTagAsync(EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                organizationId,
                existingIntegration.Type));
        Assert.Equal(updatedIntegration, result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_IntegrationDoesNotExist_ThrowsNotFound(
        SutProvider<UpdateOrganizationIntegrationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegration updatedIntegration)
    {
        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns((OrganizationIntegration)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, integrationId, updatedIntegration));

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceive()
            .ReplaceAsync(Arg.Any<OrganizationIntegration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_IntegrationDoesNotBelongToOrganization_ThrowsNotFound(
        SutProvider<UpdateOrganizationIntegrationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegration existingIntegration,
        OrganizationIntegration updatedIntegration)
    {
        existingIntegration.Id = integrationId;
        existingIntegration.OrganizationId = Guid.NewGuid(); // Different organization

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(existingIntegration);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, integrationId, updatedIntegration));

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceive()
            .ReplaceAsync(Arg.Any<OrganizationIntegration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_IntegrationIsDifferentType_ThrowsNotFound(
        SutProvider<UpdateOrganizationIntegrationCommand> sutProvider,
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegration existingIntegration,
        OrganizationIntegration updatedIntegration)
    {
        existingIntegration.Id = integrationId;
        existingIntegration.OrganizationId = organizationId;
        existingIntegration.Type = IntegrationType.Webhook;
        updatedIntegration.Id = integrationId;
        updatedIntegration.OrganizationId = organizationId;
        updatedIntegration.Type = IntegrationType.Hec; // Different Type

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByIdAsync(integrationId)
            .Returns(existingIntegration);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, integrationId, updatedIntegration));

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceive()
            .ReplaceAsync(Arg.Any<OrganizationIntegration>());
        await sutProvider.GetDependency<IFusionCache>().DidNotReceive()
            .RemoveByTagAsync(Arg.Any<string>());
    }
}
