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

        // The EF implementation can only update entities that came from a query
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
    /// The claim scans the whole shared table, so each test claims in its own random window, far in the future.
    /// Whole seconds keep the bounds exact, because Dapper rounds the stored dates.
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
