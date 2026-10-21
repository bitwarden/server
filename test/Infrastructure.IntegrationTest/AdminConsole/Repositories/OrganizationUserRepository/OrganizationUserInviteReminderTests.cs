using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.Comparers;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.OrganizationUserRepository;

public class OrganizationUserInviteReminderTests
{
    private const int BatchSize = 100;

    [Theory, DatabaseData]
    public async Task ClaimInviteReminderSequencesAsync_DueMembers_ClaimsOldestFirstUpToBatchSize(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var (now, oldestDueDate, nextDueDate) = CreateClaimWindow();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var oldest = await CreateInvitedAsync(organizationUserRepository, organization, now.AddDays(-3), 0);
        var middle = await CreateInvitedAsync(organizationUserRepository, organization, now.AddDays(-2), 1);
        var newest = await CreateInvitedAsync(organizationUserRepository, organization, now.AddDays(-1), 2);

        // Act
        var claimed = await organizationUserRepository.ClaimInviteReminderSequencesAsync(
            now, oldestDueDate, nextDueDate, 2);

        // Assert
        Assert.Equal(2, claimed.Count);
        var claimedOldest = Assert.Single(claimed, c => c.Id == oldest.Id);
        Assert.Equal(organization.Id, claimedOldest.OrganizationId);
        Assert.Equal(oldest.Email, claimedOldest.Email);
        Assert.Equal(1, claimedOldest.InviteRemindersSentCount);
        Assert.Equal(2, Assert.Single(claimed, c => c.Id == middle.Id).InviteRemindersSentCount);

        await AssertSequenceAsync(organizationUserRepository, oldest.Id, nextDueDate, 1);
        await AssertSequenceAsync(organizationUserRepository, middle.Id, nextDueDate, 2);
        await AssertSequenceAsync(organizationUserRepository, newest.Id, now.AddDays(-1), 2);
    }

    [Theory, DatabaseData]
    public async Task ClaimInviteReminderSequencesAsync_DueDateOnEitherBound_ClaimsMember(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var (now, oldestDueDate, nextDueDate) = CreateClaimWindow();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var dueNow = await CreateInvitedAsync(organizationUserRepository, organization, now, 0);
        var dueOldest = await CreateInvitedAsync(organizationUserRepository, organization, oldestDueDate, 0);

        // Act
        var claimed = await organizationUserRepository.ClaimInviteReminderSequencesAsync(
            now, oldestDueDate, nextDueDate, BatchSize);

        // Assert
        Assert.Equal(new[] { dueNow.Id, dueOldest.Id }.Order(), claimed.Select(c => c.Id).Order());
    }

    [Theory, DatabaseData]
    public async Task ClaimInviteReminderSequencesAsync_IneligibleMembers_SkipsThem(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var (now, oldestDueDate, nextDueDate) = CreateClaimWindow();
        var due = now.AddDays(-1);
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var disabledOrganization = await organizationRepository.CreateTestOrganizationAsync();
        disabledOrganization.Enabled = false;
        await organizationRepository.ReplaceAsync(disabledOrganization);
        var user = await userRepository.CreateTestUserAsync();

        var accepted = await organizationUserRepository.CreateAcceptedTestOrganizationUserAsync(organization, user);
        accepted.Email = $"{Guid.NewGuid()}@example.com";
        accepted.InviteReminderDueDate = due;
        accepted.InviteRemindersSentCount = 0;
        await organizationUserRepository.ReplaceAsync(accepted);

        var skipped = new List<OrganizationUser>
        {
            accepted,
            await CreateInvitedAsync(organizationUserRepository, organization, due, 0,
                OrganizationUserStatusType.Revoked),
            await CreateInvitedAsync(organizationUserRepository, disabledOrganization, due, 0),
            await CreateInvitedAsync(organizationUserRepository, organization, null, null),
            await CreateInvitedAsync(organizationUserRepository, organization, due, 3),
            await CreateInvitedAsync(organizationUserRepository, organization, now.AddSeconds(1), 0),
            await CreateInvitedAsync(organizationUserRepository, organization, oldestDueDate.AddSeconds(-1), 0),
            await CreateInvitedAsync(organizationUserRepository, organization, due, 0, hasEmail: false),
        };

        // Act
        var claimed = await organizationUserRepository.ClaimInviteReminderSequencesAsync(
            now, oldestDueDate, nextDueDate, BatchSize);

        // Assert
        var skippedIds = skipped.Select(ou => ou.Id).ToHashSet();
        Assert.DoesNotContain(claimed, c => skippedIds.Contains(c.Id));
        foreach (var orgUser in skipped)
        {
            await AssertSequenceAsync(organizationUserRepository, orgUser.Id,
                orgUser.InviteReminderDueDate, orgUser.InviteRemindersSentCount);
        }
    }

    [Theory, DatabaseData]
    public async Task ClaimInviteReminderSequencesAsync_SecondClaim_ReturnsNoClaimedMember(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var (now, oldestDueDate, nextDueDate) = CreateClaimWindow();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser = await CreateInvitedAsync(organizationUserRepository, organization, now.AddDays(-1), 0);

        // Act
        var first = await organizationUserRepository.ClaimInviteReminderSequencesAsync(
            now, oldestDueDate, nextDueDate, BatchSize);
        var second = await organizationUserRepository.ClaimInviteReminderSequencesAsync(
            now, oldestDueDate, nextDueDate, BatchSize);

        // Assert
        Assert.Contains(first, c => c.Id == orgUser.Id);
        Assert.DoesNotContain(second, c => c.Id == orgUser.Id);
        await AssertSequenceAsync(organizationUserRepository, orgUser.Id, nextDueDate, 1);
    }

    [Theory, DatabaseData]
    public async Task ClaimInviteReminderSequencesAsync_ConcurrentClaims_ClaimEachMemberOnce(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var (now, oldestDueDate, nextDueDate) = CreateClaimWindow();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUserIds = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            var orgUser = await CreateInvitedAsync(organizationUserRepository, organization,
                now.AddMinutes(-i), 0);
            orgUserIds.Add(orgUser.Id);
        }

        // Act
        var results = await Task.WhenAll(
            organizationUserRepository.ClaimInviteReminderSequencesAsync(now, oldestDueDate, nextDueDate, BatchSize),
            organizationUserRepository.ClaimInviteReminderSequencesAsync(now, oldestDueDate, nextDueDate, BatchSize));

        // Assert
        var claimedIds = results.SelectMany(r => r).Select(c => c.Id).ToList();
        Assert.Equal(orgUserIds.Order(), claimedIds.Order());
        Assert.Empty(results[0].Select(c => c.Id).Intersect(results[1].Select(c => c.Id)));
        foreach (var orgUserId in orgUserIds)
        {
            await AssertSequenceAsync(organizationUserRepository, orgUserId, nextDueDate, 1);
        }
    }

    [Theory, DatabaseData]
    public async Task StartInviteReminderSequencesAsync_ResetsCountAndKeepsLaterDueDate(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var (dueDate, _, _) = CreateClaimWindow();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var laterDueDate = await CreateInvitedAsync(organizationUserRepository, organization, dueDate.AddDays(1), 2);
        var earlierDueDate = await CreateInvitedAsync(organizationUserRepository, organization, dueDate.AddDays(-1), 3);
        var noDueDate = await CreateInvitedAsync(organizationUserRepository, organization, null, null);
        var notInList = await CreateInvitedAsync(organizationUserRepository, organization, dueDate.AddDays(-1), 1);
        var revisionDate = (await organizationUserRepository.GetByIdAsync(laterDueDate.Id))!.RevisionDate;

        // Act
        await organizationUserRepository.StartInviteReminderSequencesAsync(
            [laterDueDate.Id, earlierDueDate.Id, noDueDate.Id], dueDate);

        // Assert
        await AssertSequenceAsync(organizationUserRepository, laterDueDate.Id, dueDate.AddDays(1), 0);
        await AssertSequenceAsync(organizationUserRepository, earlierDueDate.Id, dueDate, 0);
        await AssertSequenceAsync(organizationUserRepository, noDueDate.Id, dueDate, 0);
        await AssertSequenceAsync(organizationUserRepository, notInList.Id, dueDate.AddDays(-1), 1);
        Assert.Equal(revisionDate, (await organizationUserRepository.GetByIdAsync(laterDueDate.Id))!.RevisionDate);
    }

    [Theory, DatabaseData]
    public async Task ReplaceAsync_SavesInviteReminderColumns(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var (dueDate, _, _) = CreateClaimWindow();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser = await CreateInvitedAsync(organizationUserRepository, organization, null, null);
        orgUser.InviteReminderDueDate = dueDate;
        orgUser.InviteRemindersSentCount = 2;

        // Act
        await organizationUserRepository.ReplaceAsync(orgUser);

        // Assert
        await AssertSequenceAsync(organizationUserRepository, orgUser.Id, dueDate, 2);
    }

    [Theory, DatabaseData]
    public async Task ReplaceManyAsync_SavesInviteReminderColumns(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var (dueDate, _, _) = CreateClaimWindow();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var cleared = await CreateInvitedAsync(organizationUserRepository, organization, dueDate, 1);
        var set = await CreateInvitedAsync(organizationUserRepository, organization, null, null);

        // EF can only update entities that it read from the database
        var toUpdate = await organizationUserRepository.GetManyAsync([cleared.Id, set.Id]);
        foreach (var orgUser in toUpdate)
        {
            var isCleared = orgUser.Id == cleared.Id;
            orgUser.InviteReminderDueDate = isCleared ? null : dueDate;
            orgUser.InviteRemindersSentCount = isCleared ? null : (byte)2;
        }

        // Act
        await organizationUserRepository.ReplaceManyAsync(toUpdate);

        // Assert
        await AssertSequenceAsync(organizationUserRepository, cleared.Id, null, null);
        await AssertSequenceAsync(organizationUserRepository, set.Id, dueDate, 2);
    }

    [Theory, DatabaseData]
    public async Task CreateAndReplaceWithCollections_SaveInviteReminderColumns(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var (dueDate, _, _) = CreateClaimWindow();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser = NewInvited(organization, dueDate, 0);

        // Act
        await organizationUserRepository.CreateAsync(orgUser, new List<CollectionAccessSelection>());

        // Assert
        await AssertSequenceAsync(organizationUserRepository, orgUser.Id, dueDate, 0);

        // Act
        orgUser.InviteReminderDueDate = dueDate.AddDays(5);
        orgUser.InviteRemindersSentCount = 1;
        await organizationUserRepository.ReplaceAsync(orgUser, new List<CollectionAccessSelection>());

        // Assert
        await AssertSequenceAsync(organizationUserRepository, orgUser.Id, dueDate.AddDays(5), 1);
    }

    [Theory, DatabaseData]
    public async Task CreateManyAsync_SavesInviteReminderColumns(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var (dueDate, _, _) = CreateClaimWindow();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var withCollections = NewInvited(organization, dueDate, 0);
        withCollections.Id = CoreHelpers.GenerateComb();

        // Act
        var ids = await organizationUserRepository.CreateManyAsync([NewInvited(organization, dueDate, 1)]);
        await organizationUserRepository.CreateManyAsync([
            new CreateOrganizationUser { OrganizationUser = withCollections }
        ]);

        // Assert
        Assert.NotNull(ids);
        await AssertSequenceAsync(organizationUserRepository, Assert.Single(ids), dueDate, 1);
        await AssertSequenceAsync(organizationUserRepository, withCollections.Id, dueDate, 0);
    }

    /// <summary>
    /// The claim reads the full shared table. Thus, each test uses its own random date range in the future.
    /// Whole seconds keep the limits exact, because Dapper rounds the stored dates.
    /// </summary>
    private static (DateTime Now, DateTime OldestDueDate, DateTime NextDueDate) CreateClaimWindow()
    {
        var now = DateTime.UtcNow.AddDays(Random.Shared.Next(36_500, 2_000_000));
        now = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, now.Kind);
        return (now, now.AddDays(-4), now.AddDays(5));
    }

    private static Task<OrganizationUser> CreateInvitedAsync(
        IOrganizationUserRepository organizationUserRepository,
        Organization organization,
        DateTime? dueDate,
        byte? sentCount,
        OrganizationUserStatusType status = OrganizationUserStatusType.Invited,
        bool hasEmail = true)
    {
        var orgUser = NewInvited(organization, dueDate, sentCount);
        orgUser.Status = status;
        orgUser.Email = hasEmail ? orgUser.Email : null;
        return organizationUserRepository.CreateAsync(orgUser);
    }

    private static OrganizationUser NewInvited(Organization organization, DateTime? dueDate, byte? sentCount)
        => new()
        {
            OrganizationId = organization.Id,
            Email = $"{Guid.NewGuid()}@example.com",
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.User,
            InviteReminderDueDate = dueDate,
            InviteRemindersSentCount = sentCount
        };

    private static async Task AssertSequenceAsync(
        IOrganizationUserRepository organizationUserRepository,
        Guid organizationUserId,
        DateTime? expectedDueDate,
        byte? expectedSentCount)
    {
        var orgUser = await organizationUserRepository.GetByIdAsync(organizationUserId);
        Assert.NotNull(orgUser);
        if (expectedDueDate is null)
        {
            Assert.Null(orgUser.InviteReminderDueDate);
        }
        else
        {
            Assert.NotNull(orgUser.InviteReminderDueDate);
            Assert.Equal(expectedDueDate.Value, orgUser.InviteReminderDueDate.Value, LaxDateTimeComparer.Default);
        }
        Assert.Equal(expectedSentCount, orgUser.InviteRemindersSentCount);
    }
}
