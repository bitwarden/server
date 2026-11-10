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
public class ReplaceOrganizationAndUpdateCacheCommandTests
{
    [Theory, BitAutoData]
    public async Task RunAsync_SavesThenRefreshesTheCache(
        Organization organization,
        SutProvider<ReplaceOrganizationAndUpdateCacheCommand> sutProvider)
    {
        await sutProvider.Sut.RunAsync(organization);

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationAbilityCacheService = sutProvider.GetDependency<IOrganizationAbilityCacheService>();
        Received.InOrder(() =>
        {
            organizationRepository.ReplaceAsync(organization);
            organizationAbilityCacheService.UpsertOrganizationAbilityAsync(organization);
        });
    }

    [Theory, BitAutoData]
    public async Task RunAsync_NoEvent_LogsNothing(
        Organization organization,
        SutProvider<ReplaceOrganizationAndUpdateCacheCommand> sutProvider)
    {
        await sutProvider.Sut.RunAsync(organization);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationEventAsync(default(Organization)!, default);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WithEvent_LogsTheEvent(
        Organization organization,
        SutProvider<ReplaceOrganizationAndUpdateCacheCommand> sutProvider)
    {
        await sutProvider.Sut.RunAsync(organization, EventType.Organization_Updated);

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationEventAsync(organization, EventType.Organization_Updated);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_SaveFails_RethrowsWithoutRefreshingTheCache(
        Organization organization,
        SutProvider<ReplaceOrganizationAndUpdateCacheCommand> sutProvider)
    {
        var saveFailure = new InvalidOperationException("The save failed.");
        sutProvider.GetDependency<IOrganizationRepository>()
            .ReplaceAsync(organization)
            .ThrowsAsync(saveFailure);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sutProvider.Sut.RunAsync(organization));

        Assert.Same(saveFailure, exception);
        await sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .DidNotReceiveWithAnyArgs()
            .UpsertOrganizationAbilityAsync(default!);
    }
}
