using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
[OrganizationCustomize]
public class CollectionServiceTest
{
    [Theory, BitAutoData]
    public async Task DeleteUserAsync_DeletesValidUserWhoBelongsToCollection(Collection collection,
        Organization organization, OrganizationUser organizationUser, SutProvider<CollectionService> sutProvider)
    {
        collection.OrganizationId = organization.Id;
        organizationUser.OrganizationId = organization.Id;
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        await sutProvider.Sut.DeleteUserAsync(collection, organizationUser.Id);

        await sutProvider.GetDependency<ICollectionRepository>().Received()
            .DeleteUserAsync(collection.Id, organizationUser.Id);
        await sutProvider.GetDependency<IEventService>().Received().LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Updated);
    }

    [Theory, BitAutoData]
    public async Task DeleteUserAsync_InvalidUser_ThrowsNotFound(Collection collection, Organization organization,
        OrganizationUser organizationUser, SutProvider<CollectionService> sutProvider)
    {
        collection.OrganizationId = organization.Id;
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        // user not in organization
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.DeleteUserAsync(collection, organizationUser.Id));
        // invalid user
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteUserAsync(collection, Guid.NewGuid()));
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().DeleteUserAsync(default, default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync<OrganizationUser>(default, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteUserAsync_WithDefaultUserCollectionType_ThrowsBadRequest(Collection collection,
        Organization organization, OrganizationUser organizationUser, SutProvider<CollectionService> sutProvider)
    {
        collection.Type = CollectionType.DefaultUserCollection;
        collection.OrganizationId = organization.Id;
        organizationUser.OrganizationId = organization.Id;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.DeleteUserAsync(collection, organizationUser.Id));
        Assert.Contains("You cannot modify member access for collections with the type as DefaultUserCollection.", exception.Message);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().DeleteUserAsync(default, default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync<OrganizationUser>(default, default);
    }
}
