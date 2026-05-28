using System.Security.Cryptography;
using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Repositories;
using Bit.Infrastructure.IntegrationTest.Comparers;
using Microsoft.Extensions.Logging;
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
        Database database,
        CapturingLoggerProvider logs)
    {
        // Only the Dapper SendRepository (SQL Server) carries the truncation-aware
        // error message. EF providers use a different implementation.
        if (database.Type != SupportedDatabaseProviders.SqlServer || database.UseEf)
        {
            return;
        }

        // Insert a Send whose Emails column holds exactly NVARCHAR(4000) bytes.
        // ProtectData's own prefix check skips re-protection on a "P|"-prefixed
        // value, so the literal 4000-char string lands in [dbo].[Send].[Emails].
        // On read, Unprotect fails because the payload is not real ciphertext, and
        // UnprotectData should emit the truncation-warning message because
        // send.Emails.Length == 4000.
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
            logs.Entries,
            e => e.Level == LogLevel.Error
                 && e.Message.Contains("is max length and may have been truncated"));
    }
}
