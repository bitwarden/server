using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Scenes;
using Bit.Seeder.Services;
using NSubstitute;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class OrganizationVaultSceneGuardWiringTests
{
    private static readonly string _organizationKey = RustSdkService.GenerateOrganizationKeys().Key;

    private readonly Organization _organization = new() { Id = Guid.NewGuid() };
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly ICollectionRepository _collectionRepository = Substitute.For<ICollectionRepository>();
    private readonly IOrganizationUserRepository _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
    private readonly IGroupRepository _groupRepository = Substitute.For<IGroupRepository>();
    private readonly IEventRepository _eventRepository = Substitute.For<IEventRepository>();
    private readonly IManglerService _manglerService = Substitute.For<IManglerService>();

    public OrganizationVaultSceneGuardWiringTests()
    {
        _organizationRepository.GetByIdAsync(_organization.Id).Returns(_organization);
        _manglerService.GetMangleMap().Returns([]);
    }

    [Fact]
    public async Task OrganizationCollectionScene_ForeignOrganizationUser_ThrowsWithoutCreatingCollection()
    {
        var foreignUser = OrganizationUserIn(Guid.NewGuid());
        _organizationUserRepository.GetManyAsync(Arg.Any<IEnumerable<Guid>>()).Returns([foreignUser]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewCollectionScene().SeedAsync(CollectionRequest(users: [CollectionAccess(foreignUser.Id)])));

        Assert.Contains("not in organization", exception.Message);
        await AssertNoCollectionCreatedAsync();
    }

    [Fact]
    public async Task OrganizationCollectionScene_ForeignGroup_ThrowsWithoutCreatingCollection()
    {
        var foreignGroup = new Group { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Name = "g" };
        _groupRepository.GetManyByManyIds(Arg.Any<IEnumerable<Guid>>()).Returns([foreignGroup]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewCollectionScene().SeedAsync(CollectionRequest(groups: [CollectionAccess(foreignGroup.Id)])));

        Assert.Contains("not in organization", exception.Message);
        await AssertNoCollectionCreatedAsync();
    }

    [Fact]
    public async Task OrganizationCollectionScene_ConflictingGroupAccess_ThrowsWithoutCreatingCollection()
    {
        var group = new Group { Id = Guid.NewGuid(), OrganizationId = _organization.Id, Name = "g" };
        _groupRepository.GetManyByManyIds(Arg.Any<IEnumerable<Guid>>()).Returns([group]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewCollectionScene().SeedAsync(CollectionRequest(
                groups: [CollectionAccess(group.Id), CollectionAccess(group.Id, readOnly: true)])));

        Assert.Contains("Conflicting access settings requested for group", exception.Message);
        await AssertNoCollectionCreatedAsync();
    }

    [Fact]
    public async Task OrganizationCollectionScene_DuplicateUsersAndGroups_CreatesWithDistinctSelections()
    {
        var user = OrganizationUserIn(_organization.Id);
        var group = new Group { Id = Guid.NewGuid(), OrganizationId = _organization.Id, Name = "g" };
        _organizationUserRepository.GetManyAsync(Arg.Any<IEnumerable<Guid>>()).Returns([user]);
        _groupRepository.GetManyByManyIds(Arg.Any<IEnumerable<Guid>>()).Returns([group]);

        await NewCollectionScene().SeedAsync(CollectionRequest(
            users: [CollectionAccess(user.Id), CollectionAccess(user.Id)],
            groups: [CollectionAccess(group.Id), CollectionAccess(group.Id)]));

        await _collectionRepository.Received(1).CreateAsync(
            Arg.Any<Collection>(),
            Arg.Is<IEnumerable<CollectionAccessSelection>?>(g => g!.Single().Id == group.Id),
            Arg.Is<IEnumerable<CollectionAccessSelection>?>(u => u!.Single().Id == user.Id));
    }

    [Fact]
    public async Task OrganizationGroupScene_ForeignOrganizationUser_ThrowsWithoutCreatingGroup()
    {
        var foreignUser = OrganizationUserIn(Guid.NewGuid());
        _organizationUserRepository.GetManyAsync(Arg.Any<IEnumerable<Guid>>()).Returns([foreignUser]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewGroupScene().SeedAsync(GroupRequest(organizationUserIds: [foreignUser.Id])));

        Assert.Contains("not in organization", exception.Message);
        await AssertNoGroupCreatedAsync();
    }

    [Fact]
    public async Task OrganizationGroupScene_ForeignCollection_ThrowsWithoutCreatingGroup()
    {
        var foreignCollection = new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Name = "c" };
        _collectionRepository.GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([foreignCollection]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewGroupScene().SeedAsync(GroupRequest(collections: [GroupAccess(foreignCollection.Id)])));

        Assert.Contains("not in organization", exception.Message);
        await AssertNoGroupCreatedAsync();
    }

    [Fact]
    public async Task OrganizationGroupScene_ConflictingCollectionAccess_ThrowsWithoutCreatingGroup()
    {
        var collection = new Collection { Id = Guid.NewGuid(), OrganizationId = _organization.Id, Name = "c" };
        _collectionRepository.GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([collection]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewGroupScene().SeedAsync(GroupRequest(
                collections: [GroupAccess(collection.Id), GroupAccess(collection.Id, manage: true)])));

        Assert.Contains("Conflicting access settings requested for collection", exception.Message);
        await AssertNoGroupCreatedAsync();
    }

    [Fact]
    public async Task OrganizationGroupScene_DuplicateCollections_CreatesWithDistinctSelections()
    {
        var collection = new Collection { Id = Guid.NewGuid(), OrganizationId = _organization.Id, Name = "c" };
        _collectionRepository.GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([collection]);

        await NewGroupScene().SeedAsync(GroupRequest(
            collections: [GroupAccess(collection.Id), GroupAccess(collection.Id)]));

        await _groupRepository.Received(1).CreateAsync(
            Arg.Any<Group>(),
            Arg.Is<IEnumerable<CollectionAccessSelection>>(c => c.Single().Id == collection.Id));
    }

    [Fact]
    public async Task OrganizationEventScene_ActingUserNotConfirmedMember_ThrowsWithoutCreatingEvents()
    {
        _organizationUserRepository.GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>()).Returns([]);
        var scene = new OrganizationEventScene(
            _organizationRepository, _eventRepository, _organizationUserRepository, _manglerService);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => scene.SeedAsync(
            new OrganizationEventScene.Request { OrganizationId = _organization.Id, ActingUserId = Guid.NewGuid() }));

        Assert.Contains("not confirmed members", exception.Message);
        await _eventRepository.DidNotReceiveWithAnyArgs().CreateAsync(default(IEvent)!);
    }

    private OrganizationCollectionScene NewCollectionScene() => new(
        _organizationRepository, _collectionRepository, _organizationUserRepository, _groupRepository, _manglerService);

    private OrganizationGroupScene NewGroupScene() => new(
        _organizationRepository, _groupRepository, _organizationUserRepository, _collectionRepository, _manglerService);

    private OrganizationCollectionScene.Request CollectionRequest(
        OrganizationCollectionScene.AccessSelectionRequest[]? users = null,
        OrganizationCollectionScene.AccessSelectionRequest[]? groups = null) => new()
        {
            OrganizationId = _organization.Id,
            OrganizationKeyB64 = _organizationKey,
            Name = "Engineering",
            Users = users,
            Groups = groups
        };

    private OrganizationGroupScene.Request GroupRequest(
        Guid[]? organizationUserIds = null,
        OrganizationGroupScene.AccessSelectionRequest[]? collections = null) => new()
        {
            OrganizationId = _organization.Id,
            Name = "Admins",
            OrganizationUserIds = organizationUserIds,
            Collections = collections
        };

    private static OrganizationCollectionScene.AccessSelectionRequest CollectionAccess(Guid id, bool readOnly = false) =>
        new() { Id = id, ReadOnly = readOnly, HidePasswords = false, Manage = false };

    private static OrganizationGroupScene.AccessSelectionRequest GroupAccess(Guid id, bool manage = false) =>
        new() { Id = id, ReadOnly = false, HidePasswords = false, Manage = manage };

    private static OrganizationUser OrganizationUserIn(Guid organizationId) =>
        new() { Id = Guid.NewGuid(), OrganizationId = organizationId, Status = OrganizationUserStatusType.Confirmed };

    private Task AssertNoCollectionCreatedAsync() =>
        _collectionRepository.DidNotReceive().CreateAsync(
            Arg.Any<Collection>(),
            Arg.Any<IEnumerable<CollectionAccessSelection>?>(),
            Arg.Any<IEnumerable<CollectionAccessSelection>?>());

    private Task AssertNoGroupCreatedAsync() =>
        _groupRepository.DidNotReceive().CreateAsync(Arg.Any<Group>(), Arg.Any<IEnumerable<CollectionAccessSelection>>());
}
