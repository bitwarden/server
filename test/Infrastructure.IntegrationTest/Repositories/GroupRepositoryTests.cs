using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest;

public class GroupRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task GetByIdWithCollectionsAsync_Works(IGroupRepository groupRepository,
        IServiceProvider services)
    {
        var organizationUser = await services.CreateOrganizationUserAsync();

        var collections = await services.CreateManyAsync((ICollectionRepository c) =>
            c.CreateAsync(new Collection
            {
                OrganizationId = organizationUser.OrganizationId,
                Name = "Test Collection",
            }), 5);

        var groupExternalId = Guid.NewGuid().ToString();

        await groupRepository.CreateAsync(new Group
        {
            OrganizationId = organizationUser.OrganizationId,
            Name = "Test Group",
            AccessAll = true,
            ExternalId = groupExternalId,
        }, new[]
        {
            new CollectionAccessSelection
            {
                Id = collections[0].Id,
                HidePasswords = false,
                ReadOnly = true,
            },
            new CollectionAccessSelection
            {
                Id = collections[1].Id,
                HidePasswords = false,
                ReadOnly = false,
            },
        });

        var group = (await groupRepository.GetManyByOrganizationIdAsync(organizationUser.OrganizationId))
            .Single();

        var (foundGroup, groupCollections) = await groupRepository.GetByIdWithCollectionsAsync(group.Id);

        Assert.NotNull(foundGroup);
        Assert.Equal(2, groupCollections.Count);
        Assert.Contains(groupCollections,
            c => c.Id == collections[0].Id && !c.HidePasswords && c.ReadOnly);
        Assert.Contains(groupCollections,
            c => c.Id == collections[1].Id && !c.HidePasswords && !c.ReadOnly);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyCollectionsByOrganizationIdAsync_Works(IGroupRepository groupRepository,
        IServiceProvider services)
    {
        var organizationUser = await services.CreateOrganizationUserAsync();

        var collections = await services.CreateManyAsync((ICollectionRepository c) =>
            c.CreateAsync(new Collection
            {
                OrganizationId = organizationUser.OrganizationId,
                Name = "Test Collection",
            }), 5);

        var groupExternalIds = new[]
        {
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
        };

        var groups = await services.CreateManyAsync(async (IGroupRepository g, int index) =>
            {
                var externalId = groupExternalIds[index];
                await g.CreateAsync(new Group
                {
                    OrganizationId = organizationUser.OrganizationId,
                    Name = "Test Group",
                    AccessAll = true,
                    ExternalId = externalId,
                }, new[]
                {
                    new CollectionAccessSelection
                    {
                        Id = collections[index].Id,
                        HidePasswords = true,
                        ReadOnly = true,
                    }
                });

                return (await g.GetManyByOrganizationIdAsync(organizationUser.OrganizationId))
                    .Single(g => g.ExternalId == externalId);
            }, groupExternalIds.Length);

        var groupsAndCollections = await groupRepository.GetManyWithCollectionsByOrganizationIdAsync(organizationUser.OrganizationId);

        Assert.Equal(3, groupsAndCollections.Count);

        var group1 = groupsAndCollections.FirstOrDefault(t => t.Item1.ExternalId == groupExternalIds[0]);
        Assert.NotNull(group1);
        var group1Collection = Assert.Single(group1!.Item2);
        Assert.True(group1Collection.HidePasswords);
        Assert.True(group1Collection.ReadOnly);

        var group2 = groupsAndCollections.FirstOrDefault(t => t.Item1.ExternalId == groupExternalIds[1]);
        Assert.NotNull(group2);
        Assert.Single(group2!.Item2);
        var group2Collection = Assert.Single(group1!.Item2);
        Assert.True(group2Collection.HidePasswords);
        Assert.True(group2Collection.ReadOnly);

        var group3 = groupsAndCollections.FirstOrDefault(t => t.Item1.ExternalId == groupExternalIds[2]);
        Assert.NotNull(group3);
        Assert.Single(group3!.Item2);
        var group3Collection = Assert.Single(group1!.Item2);
        Assert.True(group3Collection.HidePasswords);
        Assert.True(group3Collection.ReadOnly);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_Works(IGroupRepository groupRepository,
        IUserRepository userRepository,
        IServiceProvider services,
        ICollectionRepository collectionRepository)
    {
        var organizationUser = await services.CreateOrganizationUserAsync();

        var user = await userRepository.GetByIdAsync(organizationUser.UserId!.Value);

        var starterExternalId = Guid.NewGuid().ToString();

        var starterCollections = await services.CreateManyAsync((ICollectionRepository cr, int index) =>
        {
            return cr.CreateAsync(new Collection
            {
                ExternalId = starterExternalId + index,
                OrganizationId = organizationUser.OrganizationId,
                Name = "Test Collection",
            });
        }, 4);

        var groupExternalId = Guid.NewGuid().ToString();

        await groupRepository.CreateAsync(new Group
        {
            Name = "Test Group",
            AccessAll = true,
            OrganizationId = organizationUser.OrganizationId,
            ExternalId = groupExternalId,
        }, starterCollections.Select(c => new CollectionAccessSelection
        {
            Id = c.Id,
            HidePasswords = false,
            ReadOnly = true,
        }));

        var group = (await groupRepository.GetManyByOrganizationIdAsync(organizationUser.OrganizationId))
            .Single(g => g.ExternalId == groupExternalId);

        var newExternalId = Guid.NewGuid().ToString();
        var newCollections = await services.CreateManyAsync((ICollectionRepository cr, int index) =>
            cr.CreateAsync(new Collection
            {
                ExternalId = newExternalId + index,
                OrganizationId = organizationUser.OrganizationId,
                Name = "Test Collection",
            }), 2);

        // Act
        await groupRepository.ReplaceAsync(group,
            newCollections.Select(c => new CollectionAccessSelection
            {
                Id = c.Id,
                HidePasswords = true,
                ReadOnly = true,
            }));

        // Assert
        var groupCollections = await groupRepository.GetByIdWithCollectionsAsync(group.Id);
        Assert.Equal(2, groupCollections.Item2.Count);
        Assert.All(groupCollections.Item2, c =>
        {
            Assert.True(c.HidePasswords);
            Assert.True(c.ReadOnly);
        });

        var organizationCollections = await collectionRepository.GetManyByOrganizationIdWithAccessAsync(organizationUser.OrganizationId);
        var starterCollectionDetails = organizationCollections.Where(c => c.Item1.ExternalId.StartsWith(starterExternalId));
        Assert.Equal(4, starterCollectionDetails.Count());
        Assert.All(starterCollectionDetails, d =>
        {
            var (_, groupsAndUsers) = d;
            Assert.Empty(groupsAndUsers.Users);
            Assert.Empty(groupsAndUsers.Groups);
        });

        var updatedUser = await userRepository.GetByIdAsync(user.Id);

        Assert.True(user.AccountRevisionDate < updatedUser.AccountRevisionDate);
    }
}
