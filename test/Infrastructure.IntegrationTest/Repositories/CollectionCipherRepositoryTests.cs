using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Repositories;

public class CollectionCipherRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task UpdateCollectionsAsync_Works(ICollectionCipherRepository collectionCipherRepository,
        IServiceProvider services,
        ICipherRepository cipherRepository,
        ICollectionRepository collectionRepository)
    {
        var organizationUser = await services.CreateOrganizationUserAsync();

        var originalExternalId = Guid.NewGuid().ToString();

        await collectionRepository.CreateAsync(new Collection
        {
            OrganizationId = organizationUser.OrganizationId,
            ExternalId = originalExternalId,
            Name = "Test Collection",
        },
        groups: Array.Empty<CollectionAccessSelection>(),
        users: new[]
        {
            new CollectionAccessSelection
            {
                Id = organizationUser.Id,
                HidePasswords = false,
                ReadOnly = false,
            },
        });

        var cipher = await cipherRepository.CreateAsync(new Cipher
        {
            OrganizationId = organizationUser.OrganizationId,
            Data = "",
        });

        var newExternalIds = new string[]
        {
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString()
        };

        var newCollections = await services.CreateManyAsync(async (ICollectionRepository cr, int index) =>
        {
            var externalId = newExternalIds[index];
            await cr.CreateAsync(new Collection
            {
                OrganizationId = organizationUser.OrganizationId,
                Name = "Test Collection",
                ExternalId = externalId,
            },
            groups: Array.Empty<CollectionAccessSelection>(),
            users: new[]
            {
                new CollectionAccessSelection
                {
                    Id = organizationUser.Id,
                    HidePasswords = true,
                    ReadOnly = false,
                }
            });

            return (await cr.GetManyByOrganizationIdAsync(organizationUser.OrganizationId))
                .Single(c => c.ExternalId == externalId);
        }, newExternalIds.Length);

        // Act
        await collectionCipherRepository.UpdateCollectionsAsync(cipher.Id,
            organizationUser.UserId!.Value,
            newCollections.Select(c => c.Id));

        // Assert
        var collectionCiphers = (await collectionCipherRepository.GetManyByOrganizationIdAsync(organizationUser.OrganizationId))
            .ToArray();

        Assert.Equal(2, collectionCiphers.Length);
    }

    [DatabaseTheory, DatabaseData]
    public async Task UpdateCollectionsForCiphersAsync_Works(
        ICollectionCipherRepository collectionCipherRepository,
        IUserRepository userRepository,
        IServiceProvider services,
        ICollectionRepository collectionRepository)
    {
        var organizationUser = await services.CreateOrganizationUserAsync();
        var originalUser = await userRepository.GetByIdAsync(organizationUser.UserId!.Value);

        var originalExternalId = Guid.NewGuid().ToString();

        await collectionRepository.CreateAsync(new Collection
        {
            OrganizationId = organizationUser.OrganizationId,
            ExternalId = originalExternalId,
            Name = "Test Collection",
        },
        groups: Array.Empty<CollectionAccessSelection>(),
        users: new[]
        {
            new CollectionAccessSelection
            {
                Id = organizationUser.Id,
                HidePasswords = false,
                ReadOnly = false,
            },
        });

        var ciphers = await services.CreateManyAsync((ICipherRepository cr) =>
        {
            return cr.CreateAsync(new Cipher
            {
                OrganizationId = organizationUser.OrganizationId,
                Data = "",
            });
        }, 5);

        var newExternalIds = new string[]
        {
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString()
        };

        var newCollections = await services.CreateManyAsync(async (ICollectionRepository cr, int index) =>
        {
            var externalId = newExternalIds[index];
            await cr.CreateAsync(new Collection
            {
                OrganizationId = organizationUser.OrganizationId,
                Name = "Test Collection",
                ExternalId = externalId,
            },
            groups: Array.Empty<CollectionAccessSelection>(),
            users: new[]
            {
                new CollectionAccessSelection
                {
                    Id = organizationUser.Id,
                    HidePasswords = true,
                    ReadOnly = false,
                }
            });

            return (await cr.GetManyByOrganizationIdAsync(organizationUser.OrganizationId))
                .Single(c => c.ExternalId == externalId);
        }, newExternalIds.Length);

        await collectionCipherRepository.UpdateCollectionsForCiphersAsync(
            ciphers.Select(c => c.Id),
            organizationUser.UserId!.Value,
            organizationUser.OrganizationId,
            newCollections.Select(c => c.Id));

        var collectionCiphers = await collectionCipherRepository.GetManyByOrganizationIdAsync(
            organizationUser.OrganizationId
        );

        Assert.Equal(10, collectionCiphers.Count);
        foreach (var cipher in ciphers)
        {
            Assert.Equal(2, collectionCiphers.Where(cc => cc.CipherId == cipher.Id).Count());
        }

        foreach (var collection in newCollections)
        {
            Assert.Equal(5, collectionCiphers.Where(cc => cc.CollectionId == collection.Id).Count());
        }

        var updatedUser = await userRepository.GetByIdAsync(originalUser.Id);
        Assert.NotEqual(originalUser.AccountRevisionDate, updatedUser.AccountRevisionDate);
    }
}
