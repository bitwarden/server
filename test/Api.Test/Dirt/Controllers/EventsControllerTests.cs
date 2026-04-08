using Bit.Api.Dirt.Controllers;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Dirt.Controllers;

[ControllerCustomize(typeof(EventsController))]
[SutProviderCustomize]
public class EventsControllerTests
{
    [Theory, BitAutoData]
    public async Task GetOrganizationUser_UserNotFound_ThrowsNotFound(
        SutProvider<EventsController> sutProvider,
        Guid orgId, Guid id)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(id).Returns((OrganizationUser)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationUser(orgId, id));
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationUser_UserHasNoUserId_ThrowsNotFound(
        SutProvider<EventsController> sutProvider,
        Guid orgId, Guid id)
    {
        var organizationUser = new OrganizationUser { Id = id, OrganizationId = orgId, UserId = null };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(id).Returns(organizationUser);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationUser(orgId, id));
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationUser_UserBelongsToDifferentOrganization_ThrowsNotFound(
        SutProvider<EventsController> sutProvider,
        Guid orgId, Guid differentOrgId, Guid id)
    {
        var organizationUser = new OrganizationUser { Id = id, OrganizationId = differentOrgId, UserId = Guid.NewGuid() };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(id).Returns(organizationUser);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationUser(orgId, id));
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationUser_NoAccessToEventLogs_ThrowsNotFound(
        SutProvider<EventsController> sutProvider,
        Guid orgId, Guid id)
    {
        var organizationUser = new OrganizationUser { Id = id, OrganizationId = orgId, UserId = Guid.NewGuid() };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(id).Returns(organizationUser);
        sutProvider.GetDependency<ICurrentContext>()
            .AccessEventLogs(orgId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationUser(orgId, id));
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationUser_ValidRequest_ReturnsEvents(
        SutProvider<EventsController> sutProvider,
        Guid orgId, Guid id)
    {
        var userId = Guid.NewGuid();
        var organizationUser = new OrganizationUser { Id = id, OrganizationId = orgId, UserId = userId };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(id).Returns(organizationUser);
        sutProvider.GetDependency<ICurrentContext>()
            .AccessEventLogs(orgId).Returns(true);
        sutProvider.GetDependency<IEventRepository>()
            .GetManyByOrganizationActingUserAsync(orgId, userId, Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<PageOptions>())
            .Returns(new PagedResult<IEvent>());

        await sutProvider.Sut.GetOrganizationUser(orgId, id);

        await sutProvider.GetDependency<IEventRepository>().Received(1)
            .GetManyByOrganizationActingUserAsync(orgId, userId, Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<PageOptions>());
    }

    [Theory, BitAutoData]
    public async Task GetProviderUser_UserNotFound_ThrowsNotFound(
        SutProvider<EventsController> sutProvider,
        Guid providerId, Guid id)
    {
        sutProvider.GetDependency<IProviderUserRepository>()
            .GetByIdAsync(id).Returns((ProviderUser)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetProviderUser(providerId, id));
    }

    [Theory, BitAutoData]
    public async Task GetProviderUser_UserHasNoUserId_ThrowsNotFound(
        SutProvider<EventsController> sutProvider,
        Guid providerId, Guid id)
    {
        var providerUser = new ProviderUser { Id = id, ProviderId = providerId, UserId = null };
        sutProvider.GetDependency<IProviderUserRepository>()
            .GetByIdAsync(id).Returns(providerUser);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetProviderUser(providerId, id));
    }

    [Theory, BitAutoData]
    public async Task GetProviderUser_UserBelongsToDifferentProvider_ThrowsNotFound(
        SutProvider<EventsController> sutProvider,
        Guid providerId, Guid differentProviderId, Guid id)
    {
        var providerUser = new ProviderUser { Id = id, ProviderId = differentProviderId, UserId = Guid.NewGuid() };
        sutProvider.GetDependency<IProviderUserRepository>()
            .GetByIdAsync(id).Returns(providerUser);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetProviderUser(providerId, id));
    }

    [Theory, BitAutoData]
    public async Task GetProviderUser_NoAccessToEventLogs_ThrowsNotFound(
        SutProvider<EventsController> sutProvider,
        Guid providerId, Guid id)
    {
        var providerUser = new ProviderUser { Id = id, ProviderId = providerId, UserId = Guid.NewGuid() };
        sutProvider.GetDependency<IProviderUserRepository>()
            .GetByIdAsync(id).Returns(providerUser);
        sutProvider.GetDependency<ICurrentContext>()
            .ProviderAccessEventLogs(providerId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetProviderUser(providerId, id));
    }

    [Theory, BitAutoData]
    public async Task GetProviderUser_ValidRequest_ReturnsEvents(
        SutProvider<EventsController> sutProvider,
        Guid providerId, Guid id)
    {
        var userId = Guid.NewGuid();
        var providerUser = new ProviderUser { Id = id, ProviderId = providerId, UserId = userId };
        sutProvider.GetDependency<IProviderUserRepository>()
            .GetByIdAsync(id).Returns(providerUser);
        sutProvider.GetDependency<ICurrentContext>()
            .ProviderAccessEventLogs(providerId).Returns(true);
        sutProvider.GetDependency<IEventRepository>()
            .GetManyByProviderActingUserAsync(providerId, userId, Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<PageOptions>())
            .Returns(new PagedResult<IEvent>());

        await sutProvider.Sut.GetProviderUser(providerId, id);

        await sutProvider.GetDependency<IEventRepository>().Received(1)
            .GetManyByProviderActingUserAsync(providerId, userId, Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<PageOptions>());
    }
}
