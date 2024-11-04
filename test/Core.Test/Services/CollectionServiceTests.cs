using Bit.Core.AdminConsole.Entities;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture;
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
    public async Task SaveAsync_DefaultIdWithUsers_CreatesCollectionInTheRepository(Collection collection, Organization organization, [CollectionAccessSelectionCustomize(true)] IEnumerable<CollectionAccessSelection> users, SutProvider<CollectionService> sutProvider)
    {
        collection.Id = default;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.SaveAsync(collection, null, users);

        await sutProvider.GetDependency<ICollectionRepository>().Received()
            .CreateAsync(collection, Arg.Is<List<CollectionAccessSelection>>(l => l == null),
                Arg.Is<List<CollectionAccessSelection>>(l => l.Any(i => i.Manage == true)));
        await sutProvider.GetDependency<IEventService>().Received()
            .LogCollectionEventAsync(collection, EventType.Collection_Created);
        Assert.True(collection.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_DefaultIdWithGroupsAndUsers_CreateCollectionWithGroupsAndUsersInRepository(Collection collection,
        [CollectionAccessSelectionCustomize(true)] IEnumerable<CollectionAccessSelection> groups, IEnumerable<CollectionAccessSelection> users, Organization organization, SutProvider<CollectionService> sutProvider)
    {
        collection.Id = default;
        organization.UseGroups = true;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.SaveAsync(collection, groups, users);

        await sutProvider.GetDependency<ICollectionRepository>().Received()
            .CreateAsync(collection, Arg.Is<List<CollectionAccessSelection>>(l => l.Any(i => i.Manage == true)),
                Arg.Any<List<CollectionAccessSelection>>());
        await sutProvider.GetDependency<IEventService>().Received()
            .LogCollectionEventAsync(collection, EventType.Collection_Created);
        Assert.True(collection.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_NonDefaultId_ReplacesCollectionInRepository(Collection collection, Organization organization, [CollectionAccessSelectionCustomize(true)] IEnumerable<CollectionAccessSelection> users, SutProvider<CollectionService> sutProvider)
    {
        var creationDate = collection.CreationDate;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.SaveAsync(collection, null, users);

        await sutProvider.GetDependency<ICollectionRepository>().Received().ReplaceAsync(collection,
            Arg.Is<List<CollectionAccessSelection>>(l => l == null),
            Arg.Is<List<CollectionAccessSelection>>(l => l.Any(i => i.Manage == true)));
        await sutProvider.GetDependency<IEventService>().Received()
            .LogCollectionEventAsync(collection, EventType.Collection_Updated);
        Assert.Equal(collection.CreationDate, creationDate);
        Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_OrganizationNotUseGroup_CreateCollectionWithoutGroupsInRepository(Collection collection,
        [CollectionAccessSelectionCustomize] IEnumerable<CollectionAccessSelection> groups, [CollectionAccessSelectionCustomize(true)] IEnumerable<CollectionAccessSelection> users,
        Organization organization, SutProvider<CollectionService> sutProvider)
    {
        collection.Id = default;
        organization.UseGroups = false;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.SaveAsync(collection, groups, users);

        await sutProvider.GetDependency<ICollectionRepository>().Received().CreateAsync(collection,
            Arg.Is<List<CollectionAccessSelection>>(l => l == null),
            Arg.Is<List<CollectionAccessSelection>>(l => l.Any(i => i.Manage == true)));
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
    public async Task SaveAsync_NoManageAccess_ThrowsBadRequest(Collection collection, Organization organization,
        [CollectionAccessSelectionCustomize] IEnumerable<CollectionAccessSelection> users, SutProvider<CollectionService> sutProvider)
    {
        collection.Id = default;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        organization.AllowAdminAccessToAllCollectionItems = false;

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SaveAsync(collection, null, users));
        Assert.Contains("At least one member or group must have can manage permission.", ex.Message);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default, default, default);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogCollectionEventAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_ExceedsOrganizationMaxCollections_ThrowsBadRequest(Collection collection,
        Organization organization, [CollectionAccessSelectionCustomize(true)] IEnumerable<CollectionAccessSelection> users,
        SutProvider<CollectionService> sutProvider)
    {
        collection.Id = default;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetCountByOrganizationIdAsync(organization.Id)
            .Returns(organization.MaxCollections.Value);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SaveAsync(collection, null, users));
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
            .LogOrganizationUserEventAsync<OrganizationUser>(default, default);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationCollectionsAsync_WithViewAllCollectionsTrue_ReturnsAllOrganizationCollections(
        Collection collection, Guid organizationId, Guid userId, SutProvider<CollectionService> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(new List<Collection> { collection });
        sutProvider.GetDependency<ICurrentContext>().ViewAllCollections(organizationId).Returns(true);

        var result = await sutProvider.Sut.GetOrganizationCollectionsAsync(organizationId);

        Assert.Single(result);
        Assert.Equal(collection, result.First());

        await sutProvider.GetDependency<ICollectionRepository>().Received(1).GetManyByOrganizationIdAsync(organizationId);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetManyByUserIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationCollectionsAsync_WithViewAssignedCollectionsFalse_ThrowsBadRequestException(
        Guid organizationId, SutProvider<CollectionService> sutProvider)
    {
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetOrganizationCollectionsAsync(organizationId));

        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetManyByOrganizationIdAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetManyByUserIdAsync(default);
    }
}
