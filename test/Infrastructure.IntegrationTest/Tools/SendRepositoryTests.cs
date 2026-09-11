using System.Security.Cryptography;
using Bit.Core;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Infrastructure.IntegrationTest.Comparers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Tools;

public class SendRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_Works(ISendRepository sendRepository)
    {
        var expirationDate = DateTime.UtcNow.AddDays(7);

        var createdSend = await sendRepository.CreateAsync(new Send
        {
            Data = "{\"Text\": \"2.t|t|t\"}", // TODO: EF Should enforce this
            Type = SendType.Text,
            AccessCount = 0,
            Key = "2.t|t|t", // TODO: EF should enforce this
            ExpirationDate = expirationDate,
            DeletionDate = expirationDate.AddDays(7),
        });

        Assert.NotNull(createdSend.ExpirationDate);
        Assert.Equal(expirationDate, createdSend.ExpirationDate!.Value, LaxDateTimeComparer.Default);

        var sendFromDatabase = await sendRepository.GetByIdAsync(createdSend.Id);
        Assert.NotNull(sendFromDatabase);
        Assert.Equal(expirationDate, sendFromDatabase.ExpirationDate!.Value, LaxDateTimeComparer.Default);
        Assert.Equal(SendType.Text, sendFromDatabase.Type);
        Assert.Equal(0, sendFromDatabase.AccessCount);
        Assert.Equal("2.t|t|t", sendFromDatabase.Key);
        Assert.Equal(expirationDate.AddDays(7), sendFromDatabase.DeletionDate, LaxDateTimeComparer.Default);
        Assert.Equal("{\"Text\": \"2.t|t|t\"}", sendFromDatabase.Data);
    }

    [DatabaseTheory, DatabaseData]
    // This test runs best on a fresh database and may fail on subsequent runs with other tests.
    public async Task GetByDeletionDateAsync_Works(ISendRepository sendRepository)
    {
        var deletionDate = DateTime.UtcNow.AddYears(-1);

        var shouldDeleteSend = await sendRepository.CreateAsync(new Send
        {
            Data = "{\"Text\": \"2.t|t|t\"}", // TODO: EF Should enforce this
            Type = SendType.Text,
            AccessCount = 0,
            Key = "2.t|t|t", // TODO: EF should enforce this
            DeletionDate = deletionDate.AddSeconds(-2),
        });

        var shouldKeepSend = await sendRepository.CreateAsync(new Send
        {
            Data = "{\"Text\": \"2.t|t|t\"}", // TODO: EF Should enforce this
            Type = SendType.Text,
            AccessCount = 0,
            Key = "2.t|t|t", // TODO: EF should enforce this
            DeletionDate = deletionDate.AddSeconds(2),
        });

        var toDeleteSends = await sendRepository.GetManyByDeletionDateAsync(deletionDate, 10);
        var toDeleteSend = Assert.Single(toDeleteSends);
        Assert.Equal(shouldDeleteSend.Id, toDeleteSend.Id);
    }

    [DatabaseTheory, DatabaseData]
    // This test runs best on a fresh database and may fail on subsequent runs with other tests.
    public async Task GetManyByDeletionDateAsync_RespectsBatchSizeAndOrdersByOldestFirst(
        ISendRepository sendRepository)
    {
        var deletionDate = DateTime.UtcNow.AddYears(-1);

        var oldest = await sendRepository.CreateAsync(new Send
        {
            Data = "{\"Text\": \"2.t|t|t\"}",
            Type = SendType.Text,
            Key = "2.t|t|t",
            DeletionDate = deletionDate.AddDays(-2),
        });

        var middle = await sendRepository.CreateAsync(new Send
        {
            Data = "{\"Text\": \"2.t|t|t\"}",
            Type = SendType.Text,
            Key = "2.t|t|t",
            DeletionDate = deletionDate.AddDays(-1),
        });

        await sendRepository.CreateAsync(new Send
        {
            Data = "{\"Text\": \"2.t|t|t\"}",
            Type = SendType.Text,
            Key = "2.t|t|t",
            DeletionDate = deletionDate.AddSeconds(-2),
        });

        var toDeleteSends = await sendRepository.GetManyByDeletionDateAsync(deletionDate, 2);

        Assert.Equal(2, toDeleteSends.Count);
        Assert.Contains(toDeleteSends, s => s.Id == oldest.Id);
        Assert.Contains(toDeleteSends, s => s.Id == middle.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetByIdAsync_WhenStoredEmailsIs4000Chars_LogsTruncationErrorAndThrows(
        ISendRepository sendRepository,
        FakeLogCollector logs)
    {
        // Insert a Send whose Emails column holds exactly the 4000-char maximum (all providers
        // cap Emails at MaxLength(4000)). ProtectData's own prefix check skips re-protection on a
        // "P|"-prefixed value, so the literal 4000-char string lands in the Emails column.
        // On read, Unprotect fails because the payload is not real ciphertext, and UnprotectData
        // should emit the truncation-warning message because send.Emails.Length == 4000.
        var send = await sendRepository.CreateAsync(new Send
        {
            Type = SendType.Text,
            Data = "{\"Text\": \"2.t|t|t\"}",
            Key = "2.t|t|t",
            Emails = Constants.DatabaseFieldProtectedPrefix + new string('A', 3998),
            DeletionDate = DateTime.UtcNow.AddDays(7),
        });

        await Assert.ThrowsAsync<CryptographicException>(
            () => sendRepository.GetByIdAsync(send.Id));

        Assert.Contains(
            logs.GetSnapshot(),
            e => e.Level == LogLevel.Error
                 && e.Message.Contains("is max length and may have been truncated"));
    }

    [DatabaseTheory, DatabaseData]
    // This test runs best on a fresh database and may fail on subsequent runs with other tests.
    public async Task GetManyByDeletionDateAsync_WhenStoredEmailsCannotBeUnprotected_ReturnsRowWithoutThrowing(
        ISendRepository sendRepository)
    {
        // A Send whose Emails is "P|" + garbage cannot be unprotected. The cleanup query must still
        // return it (without unprotecting) so the deletion job can remove the unrecoverable row,
        // rather than throwing and stalling the whole batch.
        var deletionDate = DateTime.UtcNow.AddYears(-1);

        var corruptSend = await sendRepository.CreateAsync(new Send
        {
            Type = SendType.Text,
            Data = "{\"Text\": \"2.t|t|t\"}",
            Key = "2.t|t|t",
            Emails = Constants.DatabaseFieldProtectedPrefix + new string('A', 3998),
            DeletionDate = deletionDate.AddSeconds(-2),
        });

        var toDeleteSends = await sendRepository.GetManyByDeletionDateAsync(deletionDate, 10);

        Assert.Contains(toDeleteSends, s => s.Id == corruptSend.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task DeleteManyAsync_MultipleUsersInOneBatch_BumpsEachDistinctUsersAccountRevisionDate(
        ISendRepository sendRepository,
        IUserRepository userRepository)
    {
        var firstUser = await userRepository.CreateAsync(new User
        {
            Name = "Test User 1",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });
        var secondUser = await userRepository.CreateAsync(new User
        {
            Name = "Test User 2",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var firstUserSend = await sendRepository.CreateAsync(new Send
        {
            UserId = firstUser.Id,
            Data = "{\"Text\": \"2.t|t|t\"}",
            Type = SendType.Text,
            Key = "2.t|t|t",
            DeletionDate = DateTime.UtcNow.AddDays(7),
        });
        var secondUserSend = await sendRepository.CreateAsync(new Send
        {
            UserId = secondUser.Id,
            Data = "{\"Text\": \"2.t|t|t\"}",
            Type = SendType.Text,
            Key = "2.t|t|t",
            DeletionDate = DateTime.UtcNow.AddDays(7),
        });

        var firstUserBeforeDelete = await userRepository.GetByIdAsync(firstUser.Id);
        var secondUserBeforeDelete = await userRepository.GetByIdAsync(secondUser.Id);
        Assert.NotNull(firstUserBeforeDelete);
        Assert.NotNull(secondUserBeforeDelete);

        await sendRepository.DeleteManyAsync(new[] { firstUserSend.Id, secondUserSend.Id });

        Assert.Null(await sendRepository.GetByIdAsync(firstUserSend.Id));
        Assert.Null(await sendRepository.GetByIdAsync(secondUserSend.Id));

        var firstUserAfterDelete = await userRepository.GetByIdAsync(firstUser.Id);
        var secondUserAfterDelete = await userRepository.GetByIdAsync(secondUser.Id);
        Assert.NotNull(firstUserAfterDelete);
        Assert.NotNull(secondUserAfterDelete);
        Assert.True(firstUserAfterDelete.AccountRevisionDate - firstUserBeforeDelete.AccountRevisionDate > TimeSpan.Zero,
            "The first user's AccountRevisionDate is expected to be changed");
        Assert.True(secondUserAfterDelete.AccountRevisionDate - secondUserBeforeDelete.AccountRevisionDate > TimeSpan.Zero,
            "The second user's AccountRevisionDate is expected to be changed");
    }

    [DatabaseTheory, DatabaseData]
    public async Task DeleteManyAsync_FileTypeSend_RecomputesUserStorage(
        ISendRepository sendRepository,
        IUserRepository userRepository)
    {
        // User_UpdateStorage / UserUpdateStorage always touches RevisionDate, even when the
        // computed byte total doesn't change — that makes it a reliable, provider-agnostic signal
        // that the [Type] = 1 (File) branch of Send_DeleteMany's cursor actually ran for this user.
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var fileSend = await sendRepository.CreateAsync(new Send
        {
            UserId = user.Id,
            Data = "{\"Size\": 100}",
            Type = SendType.File,
            Key = "2.t|t|t",
            DeletionDate = DateTime.UtcNow.AddDays(7),
        });

        var userBeforeDelete = await userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(userBeforeDelete);

        await sendRepository.DeleteManyAsync(new[] { fileSend.Id });

        var userAfterDelete = await userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(userAfterDelete);
        Assert.True(userAfterDelete.RevisionDate - userBeforeDelete.RevisionDate > TimeSpan.Zero,
            "The user's RevisionDate is expected to be changed by a storage recompute");
    }

    [DatabaseTheory, DatabaseData]
    public async Task DeleteManyAsync_TextTypeSendOnly_DoesNotRecomputeUserStorage(
        ISendRepository sendRepository,
        IUserRepository userRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var textSend = await sendRepository.CreateAsync(new Send
        {
            UserId = user.Id,
            Data = "{\"Text\": \"2.t|t|t\"}",
            Type = SendType.Text,
            Key = "2.t|t|t",
            DeletionDate = DateTime.UtcNow.AddDays(7),
        });

        var userBeforeDelete = await userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(userBeforeDelete);

        await sendRepository.DeleteManyAsync(new[] { textSend.Id });

        var userAfterDelete = await userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(userAfterDelete);
        Assert.Equal(userBeforeDelete.RevisionDate, userAfterDelete.RevisionDate);
        Assert.True(userAfterDelete.AccountRevisionDate - userBeforeDelete.AccountRevisionDate > TimeSpan.Zero,
            "The AccountRevisionDate is still expected to be bumped for a Text-type Send");
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetIdsByOrganizationIdAsync_WithOrganizationMembers_ReturnsSendIds(
        ISendRepository sendRepository,
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgMemberUser = await userRepository.CreateTestUserAsync("org-member");
        var nonMemberUser = await userRepository.CreateTestUserAsync("non-member");

        // Create an organization user to link the member to the organization
        await organizationUserRepository.CreateTestOrganizationUserAsync(organization, orgMemberUser);

        // Create a Send by the organization member
        var orgMemberSend = await sendRepository.CreateAsync(new Send
        {
            UserId = orgMemberUser.Id,
            Type = SendType.Text,
            Data = "{\"Text\": \"2.t|t|t\"}",
            Key = "2.t|t|t",
            DeletionDate = DateTime.UtcNow.AddDays(7),
        });

        // Create a Send by the non-member user (should not be returned)
        var nonMemberSend = await sendRepository.CreateAsync(new Send
        {
            UserId = nonMemberUser.Id,
            Type = SendType.Text,
            Data = "{\"Text\": \"2.t|t|t\"}",
            Key = "2.t|t|t",
            DeletionDate = DateTime.UtcNow.AddDays(7),
        });

        // Act
        var result = await sendRepository.GetIdsByOrganizationIdAsync(organization.Id);

        // Assert
        var resultList = result.ToList();
        Assert.Single(resultList);
        Assert.Contains(orgMemberSend.Id, resultList);
        Assert.DoesNotContain(nonMemberSend.Id, resultList);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetIdsByOrganizationIdAsync_ExcludesInvitedOrganizationUsers_WithNullUserId(
        ISendRepository sendRepository,
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        // Arrange — the UserId != null guard filters out org users with null UserId (invited members)
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var confirmedUser = await userRepository.CreateTestUserAsync("confirmed");

        await organizationUserRepository.CreateTestOrganizationUserAsync(organization, confirmedUser);
        await organizationUserRepository.CreateTestOrganizationUserInviteAsync(organization);

        // Create a send for the confirmed user
        var confirmedUserSend = await sendRepository.CreateAsync(new Send
        {
            UserId = confirmedUser.Id,
            Type = SendType.Text,
            Data = "{\"Text\": \"2.t|t|t\"}",
            Key = "2.t|t|t",
            DeletionDate = DateTime.UtcNow.AddDays(7),
        });

        // Create an org-owned send (UserId = null) — would match an invited org user without the guard
        var orgOwnedSend = await sendRepository.CreateAsync(new Send
        {
            UserId = null,
            Type = SendType.Text,
            Data = "{\"Text\": \"2.t|t|t\"}",
            Key = "2.t|t|t",
            DeletionDate = DateTime.UtcNow.AddDays(7),
        });

        // Act
        var result = await sendRepository.GetIdsByOrganizationIdAsync(organization.Id);

        // Assert — only the confirmed user's send is returned; the org-owned send (UserId = null) is filtered
        // by the UserId != null guard, making this test differential (removing the guard breaks the test)
        var resultList = result.ToList();
        Assert.Single(resultList);
        Assert.Contains(confirmedUserSend.Id, resultList);
        Assert.DoesNotContain(orgOwnedSend.Id, resultList);
    }
}
