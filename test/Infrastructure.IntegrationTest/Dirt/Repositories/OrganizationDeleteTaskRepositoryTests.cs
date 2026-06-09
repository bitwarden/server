using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Enums;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Dirt.Repositories;

public class OrganizationDeleteTaskRepositoryTests
{
    [Theory, DatabaseData(OnlyOn = [SupportedDatabaseProviders.SqlServer])]
    public async Task ClaimNextPendingAsync_PendingRow_ReturnsRowWithLeaseSet(
        IOrganizationDeleteTaskRepository sut)
    {
        var task = new OrganizationDeleteTask
        {
            OrganizationId = Guid.NewGuid(),
            CreationDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        await sut.CreateAsync(task);

        var claimed = await sut.ClaimNextPendingAsync();

        Assert.NotNull(claimed);
        Assert.Equal(task.Id, claimed.Id);
        Assert.NotNull(claimed.StartDate);
        Assert.NotNull(claimed.RevisionDate);
        Assert.Null(claimed.CompletedDate);
    }

    [Theory, DatabaseData(OnlyOn = [SupportedDatabaseProviders.SqlServer])]
    public async Task UpdateProgressAsync_And_UpdateCompletedAsync_UpdatesRow(
        IOrganizationDeleteTaskRepository sut, Database database)
    {
        var task = new OrganizationDeleteTask { OrganizationId = Guid.NewGuid() };
        await sut.CreateAsync(task);

        await sut.UpdateProgressAsync(task.Id, 42);
        var afterProgress = await QueryRowAsync(database.ConnectionString, task.Id);
        Assert.Equal(42, afterProgress.ItemsDeletedCount);

        await sut.UpdateCompletedAsync(task.Id);
        var afterCompletion = await QueryRowAsync(database.ConnectionString, task.Id);
        Assert.NotNull(afterCompletion.CompletedDate);
    }

    [Theory, DatabaseData(OnlyOn = [SupportedDatabaseProviders.SqlServer])]
    public async Task ClaimNextPendingAsync_ConcurrentCalls_RowClaimedOnlyOnce(
        IOrganizationDeleteTaskRepository sut)
    {
        var task = new OrganizationDeleteTask
        {
            OrganizationId = Guid.NewGuid(),
            CreationDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        await sut.CreateAsync(task);

        var results = await Task.WhenAll(
            sut.ClaimNextPendingAsync(),
            sut.ClaimNextPendingAsync());

        Assert.Equal(1, results.Count(r => r?.Id == task.Id));
    }

    [Theory, DatabaseData(OnlyOn = [SupportedDatabaseProviders.SqlServer])]
    public async Task ClaimNextPendingAsync_StaleRevisionDate_RowIsReclaimable(
        IOrganizationDeleteTaskRepository sut, Database database)
    {
        var task = new OrganizationDeleteTask
        {
            OrganizationId = Guid.NewGuid(),
            CreationDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        await sut.CreateAsync(task);

        var firstClaim = await sut.ClaimNextPendingAsync();
        Assert.NotNull(firstClaim);
        Assert.Equal(task.Id, firstClaim.Id);

        await BackdateRevisionDateAsync(database.ConnectionString, task.Id, minutes: -15);

        var secondClaim = await sut.ClaimNextPendingAsync();
        Assert.NotNull(secondClaim);
        Assert.Equal(task.Id, secondClaim.Id);
    }

    [Theory, DatabaseData(OnlyOn = [SupportedDatabaseProviders.SqlServer])]
    public async Task ClaimNextPendingAsync_FailureCountAtMax_RowNotClaimed(
        IOrganizationDeleteTaskRepository sut)
    {
        var task = new OrganizationDeleteTask
        {
            OrganizationId = Guid.NewGuid(),
            CreationDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        await sut.CreateAsync(task);

        for (var i = 0; i < 5; i++)
        {
            await sut.UpdateErrorAsync(task.Id, $"Error {i + 1}");
        }

        var claimed = await sut.ClaimNextPendingAsync();

        Assert.True(claimed == null || claimed.Id != task.Id);
    }

    private static async Task<OrganizationDeleteTask> QueryRowAsync(string connectionString, Guid id)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT [Id], [OrganizationId], [TaskType], [CreationDate], [RevisionDate], [StartDate],
                   [CompletedDate], [ItemsDeletedCount], [FailureCount], [LastError]
            FROM [dbo].[OrganizationDeleteTask]
            WHERE [Id] = @Id
            """;
        cmd.Parameters.AddWithValue("@Id", id);
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new OrganizationDeleteTask
        {
            Id = reader.GetGuid(0),
            OrganizationId = reader.GetGuid(1),
            TaskType = (Bit.Core.Dirt.Enums.OrganizationDeleteTaskType)reader.GetByte(2),
            CreationDate = reader.GetDateTime(3),
            RevisionDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
            StartDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
            CompletedDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
            ItemsDeletedCount = reader.GetInt64(7),
            FailureCount = reader.GetInt32(8),
            LastError = reader.IsDBNull(9) ? null : reader.GetString(9),
        };
    }

    private static async Task BackdateRevisionDateAsync(string connectionString, Guid id, int minutes)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE [dbo].[OrganizationDeleteTask]
            SET [RevisionDate] = DATEADD(MINUTE, @Minutes, SYSUTCDATETIME())
            WHERE [Id] = @Id
            """;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Minutes", minutes);
        await cmd.ExecuteNonQueryAsync();
    }
}
