using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

public class CreateDefaultCollectionsTests
{
    /// <summary>
    /// Test that CreateDefaultCollectionsAsync successfully creates default collections for new users
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task UpsertDefaultCollectionsAsync_CreatesDefaultCollections_Success(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        // Arrange
        var user1 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 1",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var user2 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 2",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test Plan",
            BillingEmail = "billing@email.com"
        });

        var orgUser1 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user1.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        var orgUser2 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user2.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        // Act
        await collectionRepository.CreateDefaultCollectionsAsync(
            organization.Id,
            new[] { orgUser1.Id, orgUser2.Id },
            "My Items");

        // Assert
        var collections = await collectionRepository.GetManyByOrganizationIdAsync(organization.Id);
        var defaultCollections = collections.Where(c => c.Type == CollectionType.DefaultUserCollection).ToList();

        Assert.Equal(2, defaultCollections.Count);
        Assert.All(defaultCollections, c => Assert.Equal("My Items", c.Name));
        Assert.All(defaultCollections, c => Assert.Equal(organization.Id, c.OrganizationId));
    }

    /// <summary>
    /// Test that calling CreateDefaultCollectionsAsync multiple times does NOT create duplicates
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task UpsertDefaultCollectionsAsync_CalledMultipleTimes_DoesNotCreateDuplicates(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test Plan",
            BillingEmail = "billing@email.com"
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        // Act - Call twice
        await collectionRepository.CreateDefaultCollectionsAsync(
            organization.Id,
            new[] { orgUser.Id },
            "My Items");

        // Second call should not create duplicate
        await Assert.ThrowsAnyAsync<Exception>(() =>
            collectionRepository.CreateDefaultCollectionsAsync(
                organization.Id,
                new[] { orgUser.Id },
                "My Items"));

        // Assert - Only one collection should exist
        var collections = await collectionRepository.GetManyByOrganizationIdAsync(organization.Id);
        var defaultCollections = collections.Where(c => c.Type == CollectionType.DefaultUserCollection).ToList();

        Assert.Single(defaultCollections);
    }

    /// <summary>
    /// Test that UpsertDefaultCollectionsBulkAsync creates semaphores before collections
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task UpsertDefaultCollectionsBulkAsync_CreatesSemaphoresBeforeCollections_Success(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository,
        DatabaseContext databaseContext)
    {
        // Arrange
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test Plan",
            BillingEmail = "billing@email.com"
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        // Act
        await collectionRepository.UpsertDefaultCollectionsBulkAsync(
            organization.Id,
            new[] { orgUser.Id },
            "My Items");

        // Assert - Verify semaphore was created
        var semaphore = await databaseContext.DefaultCollectionSemaphores
            .FirstOrDefaultAsync(s => s.OrganizationId == organization.Id && s.OrganizationUserId == orgUser.Id);

        Assert.NotNull(semaphore);
        Assert.Equal(organization.Id, semaphore.OrganizationId);
        Assert.Equal(orgUser.Id, semaphore.OrganizationUserId);

        // Verify collection was created
        var collections = await collectionRepository.GetManyByOrganizationIdAsync(organization.Id);
        var defaultCollections = collections.Where(c => c.Type == CollectionType.DefaultUserCollection).ToList();

        Assert.Single(defaultCollections);
    }

    /// <summary>
    /// Test that deleting an OrganizationUser cascades to DefaultCollectionSemaphore
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task DeleteOrganizationUser_CascadesToSemaphore_Success(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository,
        DatabaseContext databaseContext)
    {
        // Arrange
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test Plan",
            BillingEmail = "billing@email.com"
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        await collectionRepository.CreateDefaultCollectionsAsync(
            organization.Id,
            new[] { orgUser.Id },
            "My Items");

        // Verify semaphore exists
        var semaphoreBefore = await databaseContext.DefaultCollectionSemaphores
            .FirstOrDefaultAsync(s => s.OrganizationUserId == orgUser.Id);
        Assert.NotNull(semaphoreBefore);

        // Act - Delete organization user
        await organizationUserRepository.DeleteAsync(orgUser);

        // Assert - Semaphore should be cascade deleted
        var semaphoreAfter = await databaseContext.DefaultCollectionSemaphores
            .FirstOrDefaultAsync(s => s.OrganizationUserId == orgUser.Id);
        Assert.Null(semaphoreAfter);
    }

    /// <summary>
    /// Test that deleting an Organization cascades through OrganizationUser to DefaultCollectionSemaphore
    /// Note: Cascade path is Organization -> OrganizationUser -> DefaultCollectionSemaphore (not direct)
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task DeleteOrganization_CascadesThroughOrganizationUser_Success(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository,
        DatabaseContext databaseContext)
    {
        // Arrange
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test Plan",
            BillingEmail = "billing@email.com"
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        await collectionRepository.CreateDefaultCollectionsAsync(
            organization.Id,
            new[] { orgUser.Id },
            "My Items");

        // Verify semaphore exists
        var semaphoreBefore = await databaseContext.DefaultCollectionSemaphores
            .FirstOrDefaultAsync(s => s.OrganizationId == organization.Id);
        Assert.NotNull(semaphoreBefore);

        // Act - Delete organization (which cascades to OrganizationUser, which cascades to semaphore)
        await organizationRepository.DeleteAsync(organization);

        // Assert - Semaphore should be cascade deleted via OrganizationUser
        var semaphoreAfter = await databaseContext.DefaultCollectionSemaphores
            .FirstOrDefaultAsync(s => s.OrganizationId == organization.Id);
        Assert.Null(semaphoreAfter);
    }

    /// <summary>
    /// Test that CreateDefaultCollectionsAsync with empty user list does nothing
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task UpsertDefaultCollectionsAsync_WithEmptyList_DoesNothing(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test Plan",
            BillingEmail = "billing@email.com"
        });

        // Act
        await collectionRepository.CreateDefaultCollectionsAsync(
            organization.Id,
            Array.Empty<Guid>(),
            "My Items");

        // Assert - No collections should be created
        var collections = await collectionRepository.GetManyByOrganizationIdAsync(organization.Id);
        Assert.Empty(collections);
    }

    /// <summary>
    /// Test that CreateDefaultCollectionsAsync creates CollectionUser entries with correct permissions
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task UpsertDefaultCollectionsAsync_CreatesCollectionUsersWithCorrectPermissions(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test Plan",
            BillingEmail = "billing@email.com"
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        // Act
        await collectionRepository.CreateDefaultCollectionsAsync(
            organization.Id,
            new[] { orgUser.Id },
            "My Items");

        // Assert
        var collections = await collectionRepository.GetManyByOrganizationIdAsync(organization.Id);
        var defaultCollection = collections.First(c => c.Type == CollectionType.DefaultUserCollection);

        var collectionUsers = await collectionRepository.GetManyUsersByIdAsync(defaultCollection.Id);
        var collectionUser = collectionUsers.Single();

        Assert.Equal(orgUser.Id, collectionUser.Id);
        Assert.False(collectionUser.ReadOnly);
        Assert.False(collectionUser.HidePasswords);
        Assert.True(collectionUser.Manage);
    }
}
