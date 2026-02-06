using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Vault.Repositories;

public class CollectionCipherRepositoryTests
{
    [Theory, DatabaseData]
    public async Task GetManySharedByOrganizationIdAsync_OnlyReturnsSharedCollections(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        ICipherRepository cipherRepository,
        ICollectionCipherRepository collectionCipherRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Enterprise",
            BillingEmail = "billing@example.com"
        });

        var sharedCollection = await collectionRepository.CreateAsync(new Collection
        {
            Name = "Shared Collection",
            OrganizationId = organization.Id,
            Type = CollectionType.SharedCollection
        });

        var defaultUserCollection = await collectionRepository.CreateAsync(new Collection
        {
            Name = "Default User Collection",
            OrganizationId = organization.Id,
            Type = CollectionType.DefaultUserCollection
        });

        var sharedCipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = ""
        });

        var defaultCipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = ""
        });

        await collectionCipherRepository.AddCollectionsForManyCiphersAsync(
            organization.Id,
            new[] { sharedCipher.Id },
            new[] { sharedCollection.Id });

        await collectionCipherRepository.AddCollectionsForManyCiphersAsync(
            organization.Id,
            new[] { defaultCipher.Id },
            new[] { defaultUserCollection.Id });

        // Act
        var result = await collectionCipherRepository.GetManySharedByOrganizationIdAsync(organization.Id);

        // Assert
        Assert.Single(result);
        Assert.Equal(sharedCollection.Id, result.First().CollectionId);
        Assert.DoesNotContain(result, cc => cc.CollectionId == defaultUserCollection.Id);

        // Cleanup
        await cipherRepository.DeleteAsync(sharedCipher);
        await cipherRepository.DeleteAsync(defaultCipher);
        await collectionRepository.DeleteAsync(sharedCollection);
        await collectionRepository.DeleteAsync(defaultUserCollection);
        await organizationRepository.DeleteAsync(organization);
    }
}
