using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
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
    public async Task InitializeOrganizationAsync_UpdatesOrgAndOrgUserAtomically(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var (user, organization, organizationUser) = await CreatePendingOrganizationWithUserAsync(
            userRepository, organizationRepository, organizationUserRepository);

        var publicKey = "public-key";
        var privateKey = "private-key";
        var userKey = "user-key";

        organization.Enabled = true;
        organization.Status = OrganizationStatusType.Created;
        organization.PublicKey = publicKey;
        organization.PrivateKey = privateKey;
        organization.RevisionDate = DateTime.UtcNow;

        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        organizationUser.UserId = user.Id;
        organizationUser.Key = userKey;
        organizationUser.Email = null;

        var confirmOwnerAction = organizationUserRepository.BuildConfirmOwnerAction(organizationUser);
        await organizationRepository.InitializeOrganizationAsync(organization, confirmOwnerAction);

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
    }

    [DatabaseTheory, DatabaseData]
    public async Task InitializeOrganizationAsync_WhenOrgUserActionFails_RollsBackAllChanges(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var (user, organization, organizationUser) = await CreatePendingOrganizationWithUserAsync(
            userRepository, organizationRepository, organizationUserRepository);

        organization.Enabled = true;
        organization.Status = OrganizationStatusType.Created;
        organization.PublicKey = "public-key";
        organization.PrivateKey = "private-key";
        organization.RevisionDate = DateTime.UtcNow;

        OrganizationInitializationAction failingAction =
            (Microsoft.Data.SqlClient.SqlConnection? _, Microsoft.Data.SqlClient.SqlTransaction? _, object? __) =>
            {
                throw new Exception("Simulated failure to test rollback");
            };

        await Assert.ThrowsAsync<Exception>(async () =>
            await organizationRepository.InitializeOrganizationAsync(organization, failingAction));

        var orgAfter = await organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(orgAfter);
        Assert.False(orgAfter.Enabled);
        Assert.Equal(OrganizationStatusType.Pending, orgAfter.Status);
        Assert.Null(orgAfter.PublicKey);
        Assert.Null(orgAfter.PrivateKey);

        var orgUserAfter = await organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.NotNull(orgUserAfter);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUserAfter.Status);
        Assert.Null(orgUserAfter.UserId);
    }

    private static async Task<(User user, Organization organization, OrganizationUser organizationUser)>
        CreatePendingOrganizationWithUserAsync(
            IUserRepository userRepository,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository)
    {
        var user = await userRepository.CreateTestUserAsync();

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Pending Org {CoreHelpers.GenerateComb()}",
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
