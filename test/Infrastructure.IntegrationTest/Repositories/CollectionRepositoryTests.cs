using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Repositories;

public class CollectionRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByUserIdAsync_UserInCollection_ReturnsCollection(ICollectionRepository collectionRepository,
        IServiceProvider services)
    {
        var organizationUser = await services.CreateOrganizationUserAsync();

        var externalId = Guid.NewGuid().ToString();

        await collectionRepository.CreateAsync(new Collection
        {
            Name = "Test Collection",
            OrganizationId = organizationUser.OrganizationId,
            ExternalId = externalId,
        },
        groups: Array.Empty<CollectionAccessSelection>(),
        users: new[] { new CollectionAccessSelection { Id = organizationUser.Id, HidePasswords = true, ReadOnly = false }});

        await collectionRepository.CreateAsync(new Collection
        {
            Name = "Don't Match Collection",
            OrganizationId = organizationUser.OrganizationId,
        });

        var collections = await collectionRepository.GetManyByUserIdAsync(organizationUser.UserId!.Value);
        var collection = Assert.Single(collections);
        Assert.Equal(externalId, collection.ExternalId);
        Assert.True(collection.HidePasswords);
        Assert.False(collection.ReadOnly);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByUserIdAsync_UserInMultipleGroups_ReturnsCollection(ICollectionRepository collectionRepository,
        IGroupRepository groupRepository,
        IServiceProvider services)
    {
        var organizationUser = await services.CreateOrganizationUserAsync();

        var externalId = Guid.NewGuid().ToString();

        var group1 = await groupRepository.CreateAsync(new Group
        {
            Name = "Test Group #1",
            OrganizationId = organizationUser.OrganizationId,
        });

        var group2 = await groupRepository.CreateAsync(new Group
        {
            Name = "Test Group #2",
            OrganizationId = organizationUser.OrganizationId,
        });

        await groupRepository.UpdateUsersAsync(group1.Id, new [] { organizationUser.Id });
        await groupRepository.UpdateUsersAsync(group2.Id, new [] { organizationUser.Id });

        await collectionRepository.CreateAsync(new Collection
        {
            Name = "Test Collection",
            OrganizationId = organizationUser.OrganizationId,
            ExternalId = externalId,
        },
        // The user will be apart of two groups, one that gives them little access to the collection
        // and one that gives them fully access, via our rules, they should be given the full rights.
        groups: new []
        {
            new CollectionAccessSelection
            {
                Id = group1.Id,
                HidePasswords = true,
                ReadOnly = true,
            },
            new CollectionAccessSelection
            {
                Id = group2.Id,
                HidePasswords = false,
                ReadOnly = false,
            }
        },
        users: Array.Empty<CollectionAccessSelection>());

        var collections = await collectionRepository.GetManyByUserIdAsync(organizationUser.UserId!.Value);

        var collection = Assert.Single(collections);
        Assert.Equal(externalId, collection.ExternalId);
        Assert.False(collection.ReadOnly);
        Assert.False(collection.HidePasswords);
    }
}
