using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories;

public class OrganizationRepositoryTests
{
    [Theory, DatabaseData]
    public async Task GetManyByIdsAsync_ExistingOrganizations_ReturnsOrganizations(IOrganizationRepository organizationRepository)
    {
        var email = "test@email.com";

        var organization1 = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org 1",
            BillingEmail = email,
            Plan = "Test",
            PrivateKey = "privatekey1"
        });

        var organization2 = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org 2",
            BillingEmail = email,
            Plan = "Test",
            PrivateKey = "privatekey2"
        });

        var result = await organizationRepository.GetManyByIdsAsync([organization1.Id, organization2.Id]);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, org => org.Id == organization1.Id);
        Assert.Contains(result, org => org.Id == organization2.Id);

        // Clean up
        await organizationRepository.DeleteAsync(organization1);
        await organizationRepository.DeleteAsync(organization2);
    }

    [Theory, DatabaseData]
    public async Task GetOccupiedSeatCountByOrganizationIdAsync_WithUsersAndSponsorships_ReturnsCorrectCounts(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationSponsorshipRepository organizationSponsorshipRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        // Create users in different states
        var user1 = await userRepository.CreateTestUserAsync("test1");
        var user2 = await userRepository.CreateTestUserAsync("test2");
        var user3 = await userRepository.CreateTestUserAsync("test3");

        // Create organization users in different states
        await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user1); // Confirmed state
        await organizationUserRepository.CreateTestOrganizationUserInviteAsync(organization); // Invited state

        // Create a revoked user manually since there's no helper for it
        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user3.Id,
            Status = OrganizationUserStatusType.Revoked,
        });

        // Create sponsorships in different states
        await organizationSponsorshipRepository.CreateAsync(new OrganizationSponsorship
        {
            SponsoringOrganizationId = organization.Id,
            IsAdminInitiated = true,
            ToDelete = false,
            ValidUntil = null,
        });

        await organizationSponsorshipRepository.CreateAsync(new OrganizationSponsorship
        {
            SponsoringOrganizationId = organization.Id,
            IsAdminInitiated = true,
            ToDelete = true,
            ValidUntil = DateTime.UtcNow.AddDays(1),
        });

        await organizationSponsorshipRepository.CreateAsync(new OrganizationSponsorship
        {
            SponsoringOrganizationId = organization.Id,
            IsAdminInitiated = true,
            ToDelete = true,
            ValidUntil = DateTime.UtcNow.AddDays(-1), // Expired
        });

        await organizationSponsorshipRepository.CreateAsync(new OrganizationSponsorship
        {
            SponsoringOrganizationId = organization.Id,
            IsAdminInitiated = false, // Not admin initiated
            ToDelete = false,
            ValidUntil = null,
        });

        // Act
        var result = await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);

        // Assert
        Assert.Equal(2, result.Users); // Confirmed + Invited users
        Assert.Equal(2, result.Sponsored); // Two valid sponsorships
        Assert.Equal(4, result.Total); // Total occupied seats
    }

    [Theory, DatabaseData]
    public async Task GetOccupiedSeatCountByOrganizationIdAsync_WithNoUsersOrSponsorships_ReturnsZero(
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        // Act
        var result = await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);

        // Assert
        Assert.Equal(0, result.Users);
        Assert.Equal(0, result.Sponsored);
        Assert.Equal(0, result.Total);
    }

    [Theory, DatabaseData]
    public async Task GetOccupiedSeatCountByOrganizationIdAsync_WithOnlyRevokedUsers_ReturnsZero(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var user = await userRepository.CreateTestUserAsync("test1");

        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Revoked,
        });

        // Act
        var result = await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);

        // Assert
        Assert.Equal(0, result.Users);
        Assert.Equal(0, result.Sponsored);
        Assert.Equal(0, result.Total);
    }

    [Theory, DatabaseData]
    public async Task GetOccupiedSeatCountByOrganizationIdAsync_WithOnlyExpiredSponsorships_ReturnsZero(
        IOrganizationRepository organizationRepository,
        IOrganizationSponsorshipRepository organizationSponsorshipRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        await organizationSponsorshipRepository.CreateAsync(new OrganizationSponsorship
        {
            SponsoringOrganizationId = organization.Id,
            IsAdminInitiated = true,
            ToDelete = true,
            ValidUntil = DateTime.UtcNow.AddDays(-1), // Expired
        });

        // Act
        var result = await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);

        // Assert
        Assert.Equal(0, result.Users);
        Assert.Equal(0, result.Sponsored);
        Assert.Equal(0, result.Total);
    }

    [Theory, DatabaseData]
    public async Task IncrementSeatCountAsync_IncrementsSeatCount(IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        organization.Seats = 5;
        await organizationRepository.ReplaceAsync(organization);

        await organizationRepository.IncrementSeatCountAsync(organization.Id, 3, DateTime.UtcNow);

        var result = await organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(result);
        Assert.Equal(8, result.Seats);
    }

    [DatabaseData, Theory]
    public async Task IncrementSeatCountAsync_GivenOrganizationHasNotChangedSeatCountBefore_WhenUpdatingOrgSeats_ThenSubscriptionUpdateIsSaved(
        IOrganizationRepository sutRepository)
    {
        // Arrange
        var organization = await sutRepository.CreateTestOrganizationAsync(seatCount: 2);
        var requestDate = DateTime.UtcNow;

        // Act
        await sutRepository.IncrementSeatCountAsync(organization.Id, 1, requestDate);

        // Assert
        var result = (await sutRepository.GetOrganizationsForSubscriptionSyncAsync()).ToArray();

        var updateResult = result.FirstOrDefault(x => x.Id == organization.Id);
        Assert.NotNull(updateResult);
        Assert.Equal(organization.Id, updateResult.Id);
        Assert.True(updateResult.SyncSeats);
        Assert.Equal(requestDate.ToString("yyyy-MM-dd HH:mm:ss"), updateResult.RevisionDate.ToString("yyyy-MM-dd HH:mm:ss"));

        // Annul
        await sutRepository.DeleteAsync(organization);
    }

    [DatabaseData, Theory]
    public async Task IncrementSeatCountAsync_GivenOrganizationHasChangedSeatCountBeforeAndRecordExists_WhenUpdatingOrgSeats_ThenSubscriptionUpdateIsSaved(
        IOrganizationRepository sutRepository)
    {
        // Arrange
        var organization = await sutRepository.CreateTestOrganizationAsync(seatCount: 2);
        await sutRepository.IncrementSeatCountAsync(organization.Id, 1, DateTime.UtcNow);

        var requestDate = DateTime.UtcNow;

        // Act
        await sutRepository.IncrementSeatCountAsync(organization.Id, 1, DateTime.UtcNow);

        // Assert
        var result = (await sutRepository.GetOrganizationsForSubscriptionSyncAsync()).ToArray();
        var updateResult = result.FirstOrDefault(x => x.Id == organization.Id);
        Assert.NotNull(updateResult);
        Assert.Equal(organization.Id, updateResult.Id);
        Assert.True(updateResult.SyncSeats);
        Assert.Equal(requestDate.ToString("yyyy-MM-dd HH:mm:ss"), updateResult.RevisionDate.ToString("yyyy-MM-dd HH:mm:ss"));

        // Annul
        await sutRepository.DeleteAsync(organization);
    }

    [DatabaseData, Theory]
    public async Task GetOrganizationsForSubscriptionSyncAsync_GivenOrganizationHasChangedSeatCount_WhenGettingOrgsToUpdate_ThenReturnsOrgSubscriptionUpdate(
        IOrganizationRepository sutRepository)
    {
        // Arrange
        var organization = await sutRepository.CreateTestOrganizationAsync(seatCount: 2);
        var requestDate = DateTime.UtcNow;
        await sutRepository.IncrementSeatCountAsync(organization.Id, 1, requestDate);

        // Act
        var result = (await sutRepository.GetOrganizationsForSubscriptionSyncAsync()).ToArray();

        // Assert
        var updateResult = result.FirstOrDefault(x => x.Id == organization.Id);
        Assert.NotNull(updateResult);
        Assert.Equal(organization.Id, updateResult.Id);
        Assert.True(updateResult.SyncSeats);
        Assert.Equal(requestDate.ToString("yyyy-MM-dd HH:mm:ss"), updateResult.RevisionDate.ToString("yyyy-MM-dd HH:mm:ss"));

        // Annul
        await sutRepository.DeleteAsync(organization);
    }

    [DatabaseData, Theory]
    public async Task UpdateSuccessfulOrganizationSyncStatusAsync_GivenOrganizationHasChangedSeatCount_WhenUpdatingStatus_ThenSuccessfullyUpdatesOrgSoItDoesntSync(
        IOrganizationRepository sutRepository)
    {
        // Arrange
        var organization = await sutRepository.CreateTestOrganizationAsync(seatCount: 2);
        var requestDate = DateTime.UtcNow;
        var syncDate = DateTime.UtcNow.AddMinutes(1);
        await sutRepository.IncrementSeatCountAsync(organization.Id, 1, requestDate);

        // Act
        await sutRepository.UpdateSuccessfulOrganizationSyncStatusAsync([organization.Id], syncDate);

        // Assert
        var result = (await sutRepository.GetOrganizationsForSubscriptionSyncAsync()).ToArray();
        Assert.Null(result.FirstOrDefault(x => x.Id == organization.Id));

        // Annul
        await sutRepository.DeleteAsync(organization);
    }

    [DatabaseTheory, DatabaseData]
    public async Task InitializePendingOrganizationAsync_Success_AllEntitiesUpdated(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        var (user, organization, organizationUser) = await CreatePendingOrganizationWithUserAsync(
            userRepository, organizationRepository, organizationUserRepository, emailVerified: false);

        var id = Guid.NewGuid();
        var publicKey = $"public-key-{id}";
        var privateKey = $"private-key-{id}";
        var userKey = $"user-key-{id}";
        var collectionName = $"Default Collection {id}";

        await organizationRepository.InitializePendingOrganizationAsync(
            organization.Id,
            publicKey,
            privateKey,
            organizationUser.Id,
            user.Id,
            userKey,
            collectionName);

        var updatedOrg = await organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(updatedOrg);
        Assert.True(updatedOrg.Enabled);
        Assert.Equal(OrganizationStatusType.Created, updatedOrg.Status);
        Assert.Equal(publicKey, updatedOrg.PublicKey);
        Assert.Equal(privateKey, updatedOrg.PrivateKey);

        var updatedOrgUser = await organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.NotNull(updatedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Confirmed, updatedOrgUser.Status);
        Assert.Equal(user.Id, updatedOrgUser.UserId);
        Assert.Equal(userKey, updatedOrgUser.Key);
        Assert.Null(updatedOrgUser.Email);

        var updatedUser = await userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.True(updatedUser.EmailVerified);

        var collections = await collectionRepository.GetManyByOrganizationIdAsync(organization.Id);
        Assert.Single(collections);
        Assert.Equal(collectionName, collections.First().Name);
    }

    [DatabaseTheory, DatabaseData]
    public async Task InitializePendingOrganizationAsync_WithoutCollection_Success(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        var (user, organization, organizationUser) = await CreatePendingOrganizationWithUserAsync(
            userRepository, organizationRepository, organizationUserRepository, emailVerified: true);

        var id = Guid.NewGuid();

        await organizationRepository.InitializePendingOrganizationAsync(
            organization.Id,
            $"public-key-{id}",
            $"private-key-{id}",
            organizationUser.Id,
            user.Id,
            $"user-key-{id}",
            null);

        var updatedOrg = await organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(updatedOrg);
        Assert.True(updatedOrg.Enabled);
        Assert.Equal(OrganizationStatusType.Created, updatedOrg.Status);

        var collections = await collectionRepository.GetManyByOrganizationIdAsync(organization.Id);
        Assert.Empty(collections);

        var updatedUser = await userRepository.GetByIdAsync(user.Id);
        Assert.True(updatedUser.EmailVerified);
    }

    [DatabaseTheory, DatabaseData]
    public async Task InitializePendingOrganizationAsync_WithInvalidOrganization_ThrowsException(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var nonExistentOrgId = Guid.NewGuid();
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await organizationRepository.InitializePendingOrganizationAsync(
                nonExistentOrgId,
                $"public-key-{id}",
                $"private-key-{id}",
                Guid.NewGuid(),
                user.Id,
                $"user-key-{id}",
                $"Collection {id}"));
    }

    [DatabaseTheory, DatabaseData]
    public async Task InitializePendingOrganizationAsync_RollbackOnError_NoChangesApplied(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var (user, organization, organizationUser) = await CreatePendingOrganizationWithUserAsync(
            userRepository, organizationRepository, organizationUserRepository, emailVerified: false);

        var id = Guid.NewGuid();
        var invalidOrgUserId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await organizationRepository.InitializePendingOrganizationAsync(
                organization.Id,
                $"public-key-{id}",
                $"private-key-{id}",
                invalidOrgUserId,
                user.Id,
                $"user-key-{id}",
                $"Collection {id}"));

        var unchangedOrg = await organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(unchangedOrg);
        Assert.False(unchangedOrg.Enabled);
        Assert.Equal(OrganizationStatusType.Pending, unchangedOrg.Status);
        Assert.Null(unchangedOrg.PublicKey);
        Assert.Null(unchangedOrg.PrivateKey);

        var unchangedUser = await userRepository.GetByIdAsync(user.Id);
        Assert.False(unchangedUser.EmailVerified);
    }

    private static async Task<(User user, Organization organization, OrganizationUser organizationUser)>
        CreatePendingOrganizationWithUserAsync(
            IUserRepository userRepository,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            bool emailVerified = false)
    {
        var id = Guid.NewGuid();

        var user = await userRepository.CreateTestUserAsync();
        user.EmailVerified = emailVerified;
        await userRepository.ReplaceAsync(user);

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Pending Org {id}",
            BillingEmail = user.Email,
            Plan = "Teams",
            Status = OrganizationStatusType.Pending,
            Enabled = false,
            PublicKey = null,
            PrivateKey = null
        });

        var organizationUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            Email = user.Email,
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.Owner
        });

        return (user, organization, organizationUser);
    }
}
