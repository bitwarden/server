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
        Organization organization, Collection collection, SutProvider<UpdateCollectionCommand> sutProvider)
    {
        var creationDate = collection.CreationDate;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
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
        Assert.Contains("You cannot edit a collection with the type as DefaultUserCollection", ex.Message);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCollectionEventAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithDefaultUserCollectionEmail_PreservesOriginalName(
        Organization organization, Collection collection, SutProvider<UpdateCollectionCommand> sutProvider)
    {
        // Arrange
        var originalName = "Original Collection Name";
        var newName = "New Collection Name";
        collection.DefaultUserCollectionEmail = "user@example.com";
        collection.Name = newName;

        var existingCollection = new Collection
        {
            Id = collection.Id,
            Name = originalName,
            DefaultUserCollectionEmail = collection.DefaultUserCollectionEmail,
            OrganizationId = collection.OrganizationId
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        // Act
        await sutProvider.Sut.UpdateAsync(collection, null, null);

        // Assert
        Assert.Equal(originalName, collection.Name);
        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .ReplaceAsync(collection, null, null);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithNullName_PreservesOriginalName(
        Organization organization, Collection collection, SutProvider<UpdateCollectionCommand> sutProvider)
    {
        // Arrange
        var originalName = "Original Collection Name";
        collection.Name = null; // Simulating null name from request model

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        // Act
        await sutProvider.Sut.UpdateAsync(collection, null, null);

        // Assert
        Assert.Equal(originalName, collection.Name);
        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .ReplaceAsync(collection, null, null);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithEmptyName_PreservesOriginalName(
        Organization organization, Collection collection, SutProvider<UpdateCollectionCommand> sutProvider)
    {
        // Arrange
        var originalName = "Original Collection Name";
        collection.Name = ""; // Simulating empty name from request model

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        // Act
        await sutProvider.Sut.UpdateAsync(collection, null, null);

        // Assert
        Assert.Equal(originalName, collection.Name);
        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .ReplaceAsync(collection, null, null);
    }
}
