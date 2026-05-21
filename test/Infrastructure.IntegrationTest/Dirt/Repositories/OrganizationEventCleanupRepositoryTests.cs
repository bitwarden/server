using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Repositories;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Dirt.Repositories;

public class OrganizationEventCleanupRepositoryTests
{
    [Theory, DatabaseData]
    public async Task ClaimNextPendingAsync_PendingRow_ReturnsRowWithLeaseSet(
        IOrganizationEventCleanupRepository sut)
    {
        var cleanup = new OrganizationEventCleanup
        {
            OrganizationId = Guid.NewGuid(),
            CreationDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        await sut.CreateAsync(cleanup);

        var claimed = await sut.ClaimNextPendingAsync();

        Assert.NotNull(claimed);
        Assert.Equal(cleanup.Id, claimed.Id);
        Assert.NotNull(claimed.StartDate);
        Assert.NotNull(claimed.RevisionDate);
        Assert.Null(claimed.CompletedDate);
    }

    [Theory, DatabaseData]
    public async Task UpdateProgressAsync_And_UpdateCompletedAsync_UpdatesRow(
        IOrganizationEventCleanupRepository sut, Database database)
    {
        var cleanup = new OrganizationEventCleanup { OrganizationId = Guid.NewGuid() };
        await sut.CreateAsync(cleanup);

        await sut.UpdateProgressAsync(cleanup.Id, 42);
        var afterProgress = await QueryRowAsync(database.ConnectionString, cleanup.Id);
        Assert.Equal(42, afterProgress.EventsDeletedCount);

        await sut.UpdateCompletedAsync(cleanup.Id);
        var afterCompletion = await QueryRowAsync(database.ConnectionString, cleanup.Id);
        Assert.NotNull(afterCompletion.CompletedDate);
    }

    [Theory, DatabaseData]
    public async Task ClaimNextPendingAsync_ConcurrentCalls_RowClaimedOnlyOnce(
        IOrganizationEventCleanupRepository sut)
    {
        var cleanup = new OrganizationEventCleanup
        {
            OrganizationId = Guid.NewGuid(),
            CreationDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        await sut.CreateAsync(cleanup);

        var results = await Task.WhenAll(
            sut.ClaimNextPendingAsync(),
            sut.ClaimNextPendingAsync());

        Assert.Equal(1, results.Count(r => r?.Id == cleanup.Id));
    }

    [Theory, DatabaseData]
    public async Task ClaimNextPendingAsync_StaleRevisionDate_RowIsReclaimable(
        IOrganizationEventCleanupRepository sut, Database database)
    {
        var cleanup = new OrganizationEventCleanup
        {
            OrganizationId = Guid.NewGuid(),
            CreationDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        await sut.CreateAsync(cleanup);

        var firstClaim = await sut.ClaimNextPendingAsync();
        Assert.NotNull(firstClaim);
        Assert.Equal(cleanup.Id, firstClaim.Id);

        await BackdateRevisionDateAsync(database.ConnectionString, cleanup.Id, minutes: -15);

        var secondClaim = await sut.ClaimNextPendingAsync();
        Assert.NotNull(secondClaim);
        Assert.Equal(cleanup.Id, secondClaim.Id);
    }

    [Theory, DatabaseData]
    public async Task ClaimNextPendingAsync_FailureCountAtMax_RowNotClaimed(
        IOrganizationEventCleanupRepository sut)
    {
        var cleanup = new OrganizationEventCleanup
        {
            OrganizationId = Guid.NewGuid(),
            CreationDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        await sut.CreateAsync(cleanup);

        for (var i = 0; i < 5; i++)
        {
            await sut.UpdateErrorAsync(cleanup.Id, $"Error {i + 1}");
        }

        var claimed = await sut.ClaimNextPendingAsync();

        Assert.True(claimed == null || claimed.Id != cleanup.Id);
    }

    private static async Task<OrganizationEventCleanup> QueryRowAsync(string connectionString, Guid id)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT [Id], [OrganizationId], [CreationDate], [RevisionDate], [StartDate],
                   [CompletedDate], [EventsDeletedCount], [FailureCount], [LastError]
            FROM [dbo].[OrganizationEventCleanup]
            WHERE [Id] = @Id
            """;
        cmd.Parameters.AddWithValue("@Id", id);
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new OrganizationEventCleanup
        {
            Id = reader.GetGuid(0),
            OrganizationId = reader.GetGuid(1),
            CreationDate = reader.GetDateTime(2),
            RevisionDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
            StartDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
            CompletedDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
            EventsDeletedCount = reader.GetInt64(6),
            FailureCount = reader.GetInt32(7),
            LastError = reader.IsDBNull(8) ? null : reader.GetString(8),
        };
    }

    private static async Task BackdateRevisionDateAsync(string connectionString, Guid id, int minutes)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE [dbo].[OrganizationEventCleanup]
            SET [RevisionDate] = DATEADD(MINUTE, @Minutes, SYSUTCDATETIME())
            WHERE [Id] = @Id
            """;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Minutes", minutes);
        await cmd.ExecuteNonQueryAsync();
    }
}
