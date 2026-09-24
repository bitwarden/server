using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
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
    }

    [Theory, DatabaseData]
    public async Task GetUserIdsByCollectionIdsAsync_IncludesOrganizationLevelUsers(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        ICipherRepository cipherRepository,
        ICollectionCipherRepository collectionCipherRepository)
    {
        // Arrange
        var ownerUser = await userRepository.CreateAsync(new User
        {
            Name = "Owner User",
            Email = $"owner+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var adminUser = await userRepository.CreateAsync(new User
        {
            Name = "Admin User",
            Email = $"admin+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var regularUser = await userRepository.CreateAsync(new User
        {
            Name = "Regular User",
            Email = $"regular+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Enterprise",
            BillingEmail = "billing@example.com",
            AllowAdminAccessToAllCollectionItems = true,
        });

        _ = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            UserId = ownerUser.Id,
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
        });

        _ = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            UserId = adminUser.Id,
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Admin,
        });

        _ = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            UserId = regularUser.Id,
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
        });

        var collection = await collectionRepository.CreateAsync(new Collection
        {
            Name = "Test Collection",
            OrganizationId = organization.Id,
        });

        var cipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = "",
        });

        await collectionCipherRepository.AddCollectionsForManyCiphersAsync(
            organization.Id,
            new[] { cipher.Id },
            new[] { collection.Id });

        // Act
        var result = await collectionCipherRepository.GetUserIdsByCollectionIdsAsync(new[] { collection.Id });

        // Assert
        Assert.Contains(ownerUser.Id, result);
        Assert.Contains(adminUser.Id, result);
        Assert.DoesNotContain(regularUser.Id, result);
    }

    [Theory, DatabaseData]
    public async Task UpdateCollectionsAsync_DoesNotRemoveCipherFromCollectionsUserCannotAccess(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        ICipherRepository cipherRepository,
        ICollectionCipherRepository collectionCipherRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"user+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Enterprise",
            BillingEmail = "billing@example.com",
        });

        var organizationUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
        });

        var accessibleCollection = new Collection { Name = "Accessible Collection", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(accessibleCollection, groups: null,
            users: new[] { new CollectionAccessSelection { Id = organizationUser.Id, ReadOnly = false } });

        var inaccessibleCollection = new Collection { Name = "Inaccessible Collection", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(inaccessibleCollection, groups: null, users: null);

        var cipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = "",
        });

        await collectionCipherRepository.AddCollectionsForManyCiphersAsync(
            organization.Id,
            new[] { cipher.Id },
            new[] { accessibleCollection.Id, inaccessibleCollection.Id });

        // The user submits an update removing the cipher from every collection they can see, which is only
        // the accessible one - the inaccessible collection should be untouched.
        await collectionCipherRepository.UpdateCollectionsAsync(cipher.Id, user.Id, Array.Empty<Guid>());

        var remainingCollectionIds = await collectionCipherRepository.GetCollectionIdsByCipherIdAsync(cipher.Id);
        Assert.DoesNotContain(accessibleCollection.Id, remainingCollectionIds);
        Assert.Contains(inaccessibleCollection.Id, remainingCollectionIds);
    }
}
