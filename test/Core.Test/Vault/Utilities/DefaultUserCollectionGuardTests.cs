using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Vault.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.Utilities;

public class DefaultUserCollectionGuardTests
{
    [Theory, BitAutoData]
    public async Task EnsureNoForeignDefaultCollectionsAsync_LoadsRequestedCollections(
        Guid organizationId, Guid userId, Collection sharedCollection)
    {
        var collectionRepository = Substitute.For<ICollectionRepository>();
        sharedCollection.OrganizationId = organizationId;
        sharedCollection.Type = CollectionType.SharedCollection;
        var collectionIds = new[] { sharedCollection.Id };
        collectionRepository.GetManyByManyIdsAsync(collectionIds).Returns(new List<Collection> { sharedCollection });

        await DefaultUserCollectionGuard.EnsureNoForeignDefaultCollectionsAsync(
            collectionRepository, organizationId, collectionIds, userId);

        await collectionRepository.Received(1).GetManyByManyIdsAsync(collectionIds);
    }

    [Theory, BitAutoData]
    public async Task EnsureNoForeignDefaultCollectionsAsync_NoDefaultCollections_DoesNotLoadOwnership(
        Guid organizationId, Guid userId, Collection sharedCollection)
    {
        var collectionRepository = Substitute.For<ICollectionRepository>();
        sharedCollection.OrganizationId = organizationId;
        sharedCollection.Type = CollectionType.SharedCollection;

        await EnsureAsync(collectionRepository, organizationId, userId, sharedCollection);

        await collectionRepository.DidNotReceiveWithAnyArgs().GetManyByUserIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task EnsureNoForeignDefaultCollectionsAsync_OwnDefaultCollection_DoesNotThrow(
        Guid organizationId, Guid userId, Collection ownDefaultCollection, Collection sharedCollection)
    {
        var collectionRepository = Substitute.For<ICollectionRepository>();
        AsDefault(ownDefaultCollection, organizationId);
        sharedCollection.OrganizationId = organizationId;
        sharedCollection.Type = CollectionType.SharedCollection;
        OwnsDefaultCollections(collectionRepository, userId, ownDefaultCollection.Id);

        await EnsureAsync(collectionRepository, organizationId, userId, ownDefaultCollection, sharedCollection);

        await collectionRepository.Received(1).GetManyByUserIdAsync(userId);
    }

    [Theory, BitAutoData]
    public async Task EnsureNoForeignDefaultCollectionsAsync_ForeignDefaultCollection_Throws(
        Guid organizationId, Guid userId, Collection foreignDefaultCollection)
    {
        var collectionRepository = Substitute.For<ICollectionRepository>();
        AsDefault(foreignDefaultCollection, organizationId);
        OwnsDefaultCollections(collectionRepository, userId);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            EnsureAsync(collectionRepository, organizationId, userId, foreignDefaultCollection));

        // A generic permission message, so the response reveals nothing about the collection.
        Assert.Equal("You do not have permissions to edit this.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task EnsureNoForeignDefaultCollectionsAsync_DefaultCollectionInAnotherOrganization_IsIgnored(
        Guid organizationId, Guid otherOrganizationId, Guid userId, Collection otherOrgDefaultCollection)
    {
        var collectionRepository = Substitute.For<ICollectionRepository>();
        AsDefault(otherOrgDefaultCollection, otherOrganizationId);

        await EnsureAsync(collectionRepository, organizationId, userId, otherOrgDefaultCollection);

        await collectionRepository.DidNotReceiveWithAnyArgs().GetManyByUserIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task EnsureNoForeignDefaultCollectionsAsync_OwnAndForeignDefaultCollections_Throws(
        Guid organizationId, Guid userId, Collection ownDefaultCollection, Collection foreignDefaultCollection)
    {
        var collectionRepository = Substitute.For<ICollectionRepository>();
        AsDefault(ownDefaultCollection, organizationId);
        AsDefault(foreignDefaultCollection, organizationId);
        OwnsDefaultCollections(collectionRepository, userId, ownDefaultCollection.Id);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            EnsureAsync(collectionRepository, organizationId, userId, ownDefaultCollection, foreignDefaultCollection));
    }

    [Theory, BitAutoData]
    public async Task GetOwnedDefaultCollectionIdsAsync_ReturnsOnlyDefaultCollections(
        Guid userId, Guid defaultCollectionId, Guid sharedCollectionId)
    {
        var collectionRepository = Substitute.For<ICollectionRepository>();
        collectionRepository.GetManyByUserIdAsync(userId).Returns(new List<CollectionDetails>
        {
            new() { Id = defaultCollectionId, Type = CollectionType.DefaultUserCollection },
            new() { Id = sharedCollectionId, Type = CollectionType.SharedCollection },
        });

        var result = await DefaultUserCollectionGuard.GetOwnedDefaultCollectionIdsAsync(collectionRepository, userId);

        Assert.Equal(new HashSet<Guid> { defaultCollectionId }, result);
    }

    /// <summary>
    /// Returns <paramref name="collections"/> from the repository and runs the guard against their Ids.
    /// </summary>
    private static Task EnsureAsync(
        ICollectionRepository collectionRepository, Guid organizationId, Guid userId, params Collection[] collections)
    {
        collectionRepository.GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections.ToList());
        return DefaultUserCollectionGuard.EnsureNoForeignDefaultCollectionsAsync(
            collectionRepository, organizationId, collections.Select(c => c.Id), userId);
    }

    private static void AsDefault(Collection collection, Guid organizationId)
    {
        collection.OrganizationId = organizationId;
        collection.Type = CollectionType.DefaultUserCollection;
    }

    private static void OwnsDefaultCollections(
        ICollectionRepository collectionRepository, Guid userId, params Guid[] defaultCollectionIds) =>
        collectionRepository.GetManyByUserIdAsync(userId).Returns(defaultCollectionIds
            .Select(id => new CollectionDetails { Id = id, Type = CollectionType.DefaultUserCollection })
            .ToList());
}
