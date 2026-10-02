using System.Security.Cryptography;
using Bit.Core;
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

        var toDeleteSends = await sendRepository.GetManyByDeletionDateAsync(deletionDate);
        var toDeleteSend = Assert.Single(toDeleteSends);
        Assert.Equal(shouldDeleteSend.Id, toDeleteSend.Id);
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

        var toDeleteSends = await sendRepository.GetManyByDeletionDateAsync(deletionDate);

        Assert.Contains(toDeleteSends, s => s.Id == corruptSend.Id);
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
