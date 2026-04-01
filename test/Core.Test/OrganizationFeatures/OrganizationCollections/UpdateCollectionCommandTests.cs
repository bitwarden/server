using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationCollections;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationCollections;

[SutProviderCustomize]
[OrganizationCustomize]
public class UpdateCollectionCommandTests
{
    [Theory, BitAutoData]
    public async Task UpdateAsync_WithoutGroupsAndUsers_ReplacesCollection(
        Organization organization, Collection collection,
        [CollectionAccessSelectionCustomize(true)] IEnumerable<CollectionAccessSelection> existingUsers,
        SutProvider<UpdateCollectionCommand> sutProvider)
    {
        organization.AllowAdminAccessToAllCollectionItems = false;
        var creationDate = collection.CreationDate;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(
                collection,
                new CollectionAccessDetails { Groups = [], Users = existingUsers }));
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.UpdateAsync(collection, null, null);

        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .ReplaceAsync(
                collection,
                Arg.Is<List<CollectionAccessSelection>>(l => l == null),
                Arg.Is<List<CollectionAccessSelection>>(l => l == null));
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogCollectionEventAsync(collection, EventType.Collection_Updated);
        Assert.Equal(collection.CreationDate, creationDate);
        Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithGroupsAndUsers_ReplacesCollectionWithGroupsAndUsers(
        Organization organization, Collection collection,
        [CollectionAccessSelectionCustomize(true)] IEnumerable<CollectionAccessSelection> groups,
        IEnumerable<CollectionAccessSelection> users,
        SutProvider<UpdateCollectionCommand> sutProvider)
    {
        var creationDate = collection.CreationDate;
        organization.UseGroups = true;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.UpdateAsync(collection, groups, users);

        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .ReplaceAsync(
                collection,
                Arg.Is<List<CollectionAccessSelection>>(l => l.Any(i => i.Manage == true)),
                Arg.Any<List<CollectionAccessSelection>>());
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogCollectionEventAsync(collection, EventType.Collection_Updated);
        Assert.Equal(collection.CreationDate, creationDate);
        Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithOrganizationUseGroupDisabled_ReplacesCollectionWithoutGroups(
        Organization organization, Collection collection,
        [CollectionAccessSelectionCustomize] IEnumerable<CollectionAccessSelection> groups,
        [CollectionAccessSelectionCustomize(true)] IEnumerable<CollectionAccessSelection> users,
        SutProvider<UpdateCollectionCommand> sutProvider)
    {
        var creationDate = collection.CreationDate;
        organization.UseGroups = false;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.UpdateAsync(collection, groups, users);

        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .ReplaceAsync(
                collection,
                Arg.Is<List<CollectionAccessSelection>>(l => l == null),
                Arg.Is<List<CollectionAccessSelection>>(l => l.Any(i => i.Manage == true)));
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogCollectionEventAsync(collection, EventType.Collection_Updated);
        Assert.Equal(collection.CreationDate, creationDate);
        Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithNonExistingOrganizationId_ThrowsBadRequest(
        Collection collection, SutProvider<UpdateCollectionCommand> sutProvider)
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateAsync(collection));
        Assert.Contains("Organization not found", ex.Message);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default, default, default);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCollectionEventAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithoutManageAccess_ThrowsBadRequest(
        Organization organization, Collection collection,
        [CollectionAccessSelectionCustomize] IEnumerable<CollectionAccessSelection> users,
        SutProvider<UpdateCollectionCommand> sutProvider)
    {
        organization.AllowAdminAccessToAllCollectionItems = false;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        // groups is null so the command will fetch existing access; return no manage access
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(
                collection,
                new CollectionAccessDetails { Groups = [], Users = [] }));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateAsync(collection, null, users));
        Assert.Contains("At least one member or group must have can manage permission.", ex.Message);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default, default, default);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCollectionEventAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithInvalidManageAssociations_ThrowsBadRequest(
        Organization organization, Collection collection, SutProvider<UpdateCollectionCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var invalidGroups = new List<CollectionAccessSelection>
        {
            new() { Id = Guid.NewGuid(), Manage = true, HidePasswords = true }
        };

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateAsync(collection, invalidGroups, null));
        Assert.Contains("The Manage property is mutually exclusive and cannot be true while the ReadOnly or HidePasswords properties are also true.", ex.Message);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default, default, default);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCollectionEventAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithDefaultUserCollectionType_ThrowsBadRequest(
        Organization organization, Collection collection, SutProvider<UpdateCollectionCommand> sutProvider)
    {
        collection.Type = CollectionType.DefaultUserCollection;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateAsync(collection));
        Assert.Contains("You cannot edit a collection with the type as DefaultUserCollection.", ex.Message);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default, default, default);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCollectionEventAsync(default, default);
    }

    /// <summary>
    /// Replication of bug: passing null groups (don't update groups) when an existing user has Can Manage
    /// should succeed, not throw.
    /// </summary>
    [Theory, BitAutoData]
    public async Task UpdateAsync_NullGroups_ExistingUserHasManage_Succeeds(
        Organization organization, Collection collection,
        [CollectionAccessSelectionCustomize(true)] IEnumerable<CollectionAccessSelection> existingUsers,
        SutProvider<UpdateCollectionCommand> sutProvider)
    {
        organization.AllowAdminAccessToAllCollectionItems = false;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(
                collection,
                new CollectionAccessDetails { Groups = [], Users = existingUsers }));

        await sutProvider.Sut.UpdateAsync(collection, null, null);

        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .ReplaceAsync(collection, Arg.Is<List<CollectionAccessSelection>>(l => l == null), Arg.Is<List<CollectionAccessSelection>>(l => l == null));
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogCollectionEventAsync(collection, EventType.Collection_Updated);
    }

    /// <summary>
    /// Replication of bug: passing groups without Can Manage when an existing user has Can Manage
    /// should succeed, not throw.
    /// </summary>
    [Theory, BitAutoData]
    public async Task UpdateAsync_NullUsers_ExistingGroupHasManage_NewGroupsLackManage_Throws(
        Organization organization, Collection collection,
        [CollectionAccessSelectionCustomize] IEnumerable<CollectionAccessSelection> newGroups,
        [CollectionAccessSelectionCustomize(true)] IEnumerable<CollectionAccessSelection> existingUsers,
        SutProvider<UpdateCollectionCommand> sutProvider)
    {
        organization.AllowAdminAccessToAllCollectionItems = false;
        organization.UseGroups = true;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        // users is null, so existing users are fetched — they have Can Manage
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(
                collection,
                new CollectionAccessDetails { Groups = [], Users = existingUsers }));

        // New groups have no manage, but existing users do — should succeed
        await sutProvider.Sut.UpdateAsync(collection, newGroups, null);

        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .ReplaceAsync(collection, Arg.Any<List<CollectionAccessSelection>>(), Arg.Is<List<CollectionAccessSelection>>(l => l == null));
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogCollectionEventAsync(collection, EventType.Collection_Updated);
    }

    /// <summary>
    /// Passing groups without Can Manage when existing users also have no Can Manage should throw.
    /// </summary>
    [Theory, BitAutoData]
    public async Task UpdateAsync_NullUsers_NoManageAnywhere_Throws(
        Organization organization, Collection collection,
        [CollectionAccessSelectionCustomize] IEnumerable<CollectionAccessSelection> newGroups,
        [CollectionAccessSelectionCustomize] IEnumerable<CollectionAccessSelection> existingUsers,
        SutProvider<UpdateCollectionCommand> sutProvider)
    {
        organization.AllowAdminAccessToAllCollectionItems = false;
        organization.UseGroups = true;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(
                collection,
                new CollectionAccessDetails { Groups = [], Users = existingUsers }));

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(collection, newGroups, null));
        Assert.Contains("At least one member or group must have can manage permission.", ex.Message);
    }

    /// <summary>
    /// When AllowAdminAccessToAllCollectionItems is true the manage check is skipped entirely,
    /// even with null groups/users — no call to GetByIdWithAccessAsync should occur.
    /// </summary>
    [Theory, BitAutoData]
    public async Task UpdateAsync_AdminAccessToAllItems_SkipsManageCheck(
        Organization organization, Collection collection,
        SutProvider<UpdateCollectionCommand> sutProvider)
    {
        organization.AllowAdminAccessToAllCollectionItems = true;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        await sutProvider.Sut.UpdateAsync(collection, null, null);

        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByIdWithAccessAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .ReplaceAsync(collection, null, null);
    }
}
