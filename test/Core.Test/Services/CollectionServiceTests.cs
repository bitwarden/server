using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
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
    public async Task SaveAsync_DefaultId_CreatesCollectionInTheRepository(Collection collection, Organization organization, SutProvider<CollectionService> sutProvider)
    {
        collection.Id = default;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.SaveAsync(collection);

        await sutProvider.GetDependency<ICollectionRepository>().Received().CreateAsync(collection, null, null);
        await sutProvider.GetDependency<IEventService>().Received()
            .LogCollectionEventAsync(collection, EventType.Collection_Created);
        Assert.True(collection.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_DefaultIdWithUsers_CreatesCollectionInTheRepository(Collection collection, Organization organization, IEnumerable<CollectionAccessSelection> users, SutProvider<CollectionService> sutProvider)
    {
        collection.Id = default;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.SaveAsync(collection, null, users);

        await sutProvider.GetDependency<ICollectionRepository>().Received().CreateAsync(collection, null, users);
        await sutProvider.GetDependency<IEventService>().Received()
            .LogCollectionEventAsync(collection, EventType.Collection_Created);
        Assert.True(collection.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_DefaultIdWithGroupsAndUsers_CreateCollectionWithGroupsAndUsersInRepository(Collection collection,
        IEnumerable<CollectionAccessSelection> groups, IEnumerable<CollectionAccessSelection> users, Organization organization, SutProvider<CollectionService> sutProvider)
    {
        collection.Id = default;
        organization.UseGroups = true;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.SaveAsync(collection, groups, users);

        await sutProvider.GetDependency<ICollectionRepository>().Received().CreateAsync(collection, groups, users);
        await sutProvider.GetDependency<IEventService>().Received()
            .LogCollectionEventAsync(collection, EventType.Collection_Created);
        Assert.True(collection.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_NonDefaultId_ReplacesCollectionInRepository(Collection collection, Organization organization, SutProvider<CollectionService> sutProvider)
    {
        var creationDate = collection.CreationDate;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.SaveAsync(collection);

        await sutProvider.GetDependency<ICollectionRepository>().Received().ReplaceAsync(collection, null, null);
        await sutProvider.GetDependency<IEventService>().Received()
            .LogCollectionEventAsync(collection, EventType.Collection_Updated);
        Assert.Equal(collection.CreationDate, creationDate);
        Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_OrganizationNotUseGroup_CreateCollectionWithoutGroupsInRepository(Collection collection, IEnumerable<CollectionAccessSelection> groups,
        Organization organization, SutProvider<CollectionService> sutProvider)
    {
        collection.Id = default;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.SaveAsync(collection, groups);

        await sutProvider.GetDependency<ICollectionRepository>().Received().CreateAsync(collection, null, null);
        await sutProvider.GetDependency<IEventService>().Received()
            .LogCollectionEventAsync(collection, EventType.Collection_Created);
        Assert.True(collection.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_DefaultIdWithUserId_UpdateUserInCollectionRepository(Collection collection,
        Organization organization, OrganizationUser organizationUser, SutProvider<CollectionService> sutProvider)
    {
        collection.Id = default;
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(organization.Id, organizationUser.Id)
            .Returns(organizationUser);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.SaveAsync(collection, null, null, organizationUser.Id);

        await sutProvider.GetDependency<ICollectionRepository>().Received().CreateAsync(collection, null, null);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received()
            .GetByOrganizationAsync(organization.Id, organizationUser.Id);
        await sutProvider.GetDependency<ICollectionRepository>().Received().UpdateUsersAsync(collection.Id, Arg.Any<List<CollectionAccessSelection>>());
        await sutProvider.GetDependency<IEventService>().Received()
            .LogCollectionEventAsync(collection, EventType.Collection_Created);
        Assert.True(collection.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_NonExistingOrganizationId_ThrowsBadRequest(Collection collection, SutProvider<CollectionService> sutProvider)
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SaveAsync(collection));
        Assert.Contains("Organization not found", ex.Message);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default, default, default);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogCollectionEventAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_ExceedsOrganizationMaxCollections_ThrowsBadRequest(Collection collection, Organization organization, SutProvider<CollectionService> sutProvider)
    {
        collection.Id = default;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetCountByOrganizationIdAsync(organization.Id)
            .Returns(organization.MaxCollections.Value);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SaveAsync(collection));
        Assert.Equal($@"You have reached the maximum number of collections ({organization.MaxCollections.Value}) for this organization.", ex.Message);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default, default, default);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogCollectionEventAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteUserAsync_DeletesValidUserWhoBelongsToCollection(Collection collection,
        Organization organization, OrganizationUser organizationUser, SutProvider<CollectionService> sutProvider)
    {
        collection.OrganizationId = organization.Id;
        organizationUser.OrganizationId = organization.Id;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
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
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        // user not in organization
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.DeleteUserAsync(collection, organizationUser.Id));
        // invalid user
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteUserAsync(collection, Guid.NewGuid()));
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().DeleteUserAsync(default, default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationCollectionsWithGroups_NoManagerPermissions_ThrowsNotFound(Organization organization, SutProvider<CollectionService> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ViewAssignedCollections(organization.Id).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetOrganizationCollectionsWithGroups(organization.Id));
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetManyWithGroupsByOrganizationIdAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetManyWithGroupsByUserIdAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationCollectionsWithGroups_AdminPermissions_GetsAllCollections(Organization organization, User user, SutProvider<CollectionService> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<ICurrentContext>().ViewAssignedCollections(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(true);

        await sutProvider.Sut.GetOrganizationCollectionsWithGroups(organization.Id);

        await sutProvider.GetDependency<ICollectionRepository>().Received().GetManyWithGroupsByOrganizationIdAsync(organization.Id);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetManyWithGroupsByUserIdAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationCollectionsWithGroups_ManagerPermissions_GetsAssignedCollections(Organization organization, User user, SutProvider<CollectionService> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<ICurrentContext>().ViewAssignedCollections(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationManager(organization.Id).Returns(true);

        await sutProvider.Sut.GetOrganizationCollectionsWithGroups(organization.Id);

        await sutProvider.GetDependency<ICollectionRepository>().Received().GetManyWithGroupsByUserIdAsync(user.Id, organization.Id);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetManyWithGroupsByOrganizationIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationCollectionsWithGroups_CustomUserWithAdminPermissions_GetsAllCollections(Organization organization, User user, SutProvider<CollectionService> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<ICurrentContext>().ViewAssignedCollections(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().EditAnyCollection(organization.Id).Returns(true);


        await sutProvider.Sut.GetOrganizationCollectionsWithGroups(organization.Id);

        await sutProvider.GetDependency<ICollectionRepository>().Received().GetManyWithGroupsByOrganizationIdAsync(organization.Id);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetManyWithGroupsByUserIdAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationCollectionsWithGroups_CustomUserWithManagerPermissions_GetsAssignedCollections(Organization organization, User user, SutProvider<CollectionService> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<ICurrentContext>().ViewAssignedCollections(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().EditAssignedCollections(organization.Id).Returns(true);


        await sutProvider.Sut.GetOrganizationCollectionsWithGroups(organization.Id);

        await sutProvider.GetDependency<ICollectionRepository>().Received().GetManyWithGroupsByUserIdAsync(user.Id, organization.Id);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetManyWithGroupsByOrganizationIdAsync(default);
    }
}
