using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class OrganizationReplaceCommandTests
{
    [Theory, BitAutoData]
    public async Task ReplaceAndUpdateCacheAsync_SavesThenRefreshesTheCache(
        Organization organization,
        SutProvider<OrganizationReplaceCommand> sutProvider)
    {
        await sutProvider.Sut.ReplaceAndUpdateCacheAsync(organization);

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationAbilityCacheService = sutProvider.GetDependency<IOrganizationAbilityCacheService>();
        Received.InOrder(() =>
        {
            organizationRepository.ReplaceAsync(organization);
            organizationAbilityCacheService.UpsertOrganizationAbilityAsync(organization);
        });
    }

    [Theory, BitAutoData]
    public async Task ReplaceAndUpdateCacheAsync_NoEvent_LogsNothing(
        Organization organization,
        SutProvider<OrganizationReplaceCommand> sutProvider)
    {
        await sutProvider.Sut.ReplaceAndUpdateCacheAsync(organization);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationEventAsync(default(Organization)!, default);
    }

    [Theory, BitAutoData]
    public async Task ReplaceAndUpdateCacheAsync_WithEvent_LogsTheEvent(
        Organization organization,
        SutProvider<OrganizationReplaceCommand> sutProvider)
    {
        await sutProvider.Sut.ReplaceAndUpdateCacheAsync(organization, EventType.Organization_Updated);

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationEventAsync(organization, EventType.Organization_Updated);
    }

    [Theory, BitAutoData]
    public async Task ReplaceAndUpdateCacheAsync_SaveFails_RethrowsWithoutRefreshingTheCache(
        Organization organization,
        SutProvider<OrganizationReplaceCommand> sutProvider)
    {
        var saveFailure = new InvalidOperationException("The save failed.");
        sutProvider.GetDependency<IOrganizationRepository>()
            .ReplaceAsync(organization)
            .ThrowsAsync(saveFailure);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sutProvider.Sut.ReplaceAndUpdateCacheAsync(organization));

        Assert.Same(saveFailure, exception);
        await sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .DidNotReceiveWithAnyArgs()
            .UpsertOrganizationAbilityAsync(default!);
    }
}
