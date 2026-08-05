using Bit.Core.AdminConsole.Entities;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        // ClaimNextPending advances RevisionDate to "now", past the CreationDate set on insert
        Assert.True(claimed.RevisionDate > task.CreationDate);
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

    [Theory, DatabaseData]
    public async Task DeleteAndCreateDeleteTasksAsync_DeletesOrganizationAndEnqueuesTask(
        IOrganizationRepository organizationRepository, Database database, IServiceProvider services)
    {
        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "test@example.com",
            Plan = "Test",
            PrivateKey = "privatekey",
        });

        await organizationRepository.DeleteAndCreateDeleteTasksAsync(
            organization, [OrganizationDeleteTaskType.EventsCleanup]);

        // The organization is gone and the cleanup task was enqueued in the same transaction.
        Assert.Null(await organizationRepository.GetByIdAsync(organization.Id));
        var task = await GetTaskByOrganizationIdAsync(services, database, organization.Id);
        Assert.NotNull(task);
        Assert.Equal(OrganizationDeleteTaskType.EventsCleanup, task.TaskType);
        Assert.Null(task.CompletedDate);
    }

    [Theory, DatabaseData]
    public async Task DeleteAndCreateDeleteTasksAsync_MultipleTaskTypes_EnqueuesOneRowPerType(
        IOrganizationRepository organizationRepository, Database database, IServiceProvider services)
    {
        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "test@example.com",
            Plan = "Test",
            PrivateKey = "privatekey",
        });

        // Only one task type exists today, so we exercise the multi-row paths (the TVP
        // INSERT...SELECT on SqlServer and the foreach on EF) by enqueuing two elements:
        // both must produce one distinct row per element, never collapsing or dropping rows.
        await organizationRepository.DeleteAndCreateDeleteTasksAsync(
            organization,
            [OrganizationDeleteTaskType.EventsCleanup, OrganizationDeleteTaskType.EventsCleanup]);

        Assert.Null(await organizationRepository.GetByIdAsync(organization.Id));
        var tasks = await ListTasksByOrganizationIdAsync(services, database, organization.Id);
        Assert.Equal(2, tasks.Count);
        Assert.All(tasks, task => Assert.Equal(OrganizationDeleteTaskType.EventsCleanup, task.TaskType));
        Assert.All(tasks, task => Assert.Null(task.CompletedDate));
        // Each enqueued task must receive a unique primary key.
        Assert.Equal(2, tasks.Select(task => task.Id).Distinct().Count());
    }

    [Theory, DatabaseData]
    public async Task DeleteAsync_DoesNotEnqueueDeleteTask(
        IOrganizationRepository organizationRepository, Database database, IServiceProvider services)
    {
        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "test@example.com",
            Plan = "Test",
            PrivateKey = "privatekey",
        });

        await organizationRepository.DeleteAsync(organization);

        // The plain delete path (e.g. signup rollback) must not enqueue a cleanup task.
        Assert.Null(await organizationRepository.GetByIdAsync(organization.Id));
        Assert.Null(await GetTaskByOrganizationIdAsync(services, database, organization.Id));
    }

    /// <summary>
    /// Reads the cleanup-task row for an organization across providers: raw SQL against the
    /// Dapper/SqlServer pair, the EF <see cref="DatabaseContext"/> everywhere else (there is
    /// no read-by-organization repository method, by design).
    /// </summary>
    private static async Task<OrganizationDeleteTask?> GetTaskByOrganizationIdAsync(
        IServiceProvider services, Database database, Guid organizationId)
    {
        if (database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf)
        {
            return await QueryRowByOrganizationIdAsync(database.ConnectionString, organizationId);
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        return await dbContext.OrganizationDeleteTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.OrganizationId == organizationId);
    }

    /// <summary>
    /// Reads all cleanup-task rows for an organization across providers, mirroring
    /// <see cref="GetTaskByOrganizationIdAsync"/> but returning every row so the
    /// multi-row enqueue path can be asserted.
    /// </summary>
    private static async Task<List<OrganizationDeleteTask>> ListTasksByOrganizationIdAsync(
        IServiceProvider services, Database database, Guid organizationId)
    {
        if (database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf)
        {
            return await QueryRowsByOrganizationIdAsync(database.ConnectionString, organizationId);
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var rows = await dbContext.OrganizationDeleteTasks
            .AsNoTracking()
            .Where(t => t.OrganizationId == organizationId)
            .ToListAsync();
        return rows.Cast<OrganizationDeleteTask>().ToList();
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
            RevisionDate = reader.GetDateTime(4),
            StartDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
            CompletedDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
            ItemsDeletedCount = reader.GetInt64(7),
            FailureCount = reader.GetInt32(8),
            LastError = reader.IsDBNull(9) ? null : reader.GetString(9),
        };
    }

    private static async Task<OrganizationDeleteTask?> QueryRowByOrganizationIdAsync(string connectionString, Guid organizationId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT [Id], [OrganizationId], [TaskType], [CreationDate], [RevisionDate], [StartDate],
                   [CompletedDate], [ItemsDeletedCount], [FailureCount], [LastError]
            FROM [dbo].[OrganizationDeleteTask]
            WHERE [OrganizationId] = @OrganizationId
            """;
        cmd.Parameters.AddWithValue("@OrganizationId", organizationId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }
        return new OrganizationDeleteTask
        {
            Id = reader.GetGuid(0),
            OrganizationId = reader.GetGuid(1),
            TaskType = (OrganizationDeleteTaskType)reader.GetByte(2),
            CreationDate = reader.GetDateTime(3),
            RevisionDate = reader.GetDateTime(4),
            StartDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
            CompletedDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
            ItemsDeletedCount = reader.GetInt64(7),
            FailureCount = reader.GetInt32(8),
            LastError = reader.IsDBNull(9) ? null : reader.GetString(9),
        };
    }

    private static async Task<List<OrganizationDeleteTask>> QueryRowsByOrganizationIdAsync(string connectionString, Guid organizationId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT [Id], [OrganizationId], [TaskType], [CreationDate], [RevisionDate], [StartDate],
                   [CompletedDate], [ItemsDeletedCount], [FailureCount], [LastError]
            FROM [dbo].[OrganizationDeleteTask]
            WHERE [OrganizationId] = @OrganizationId
            """;
        cmd.Parameters.AddWithValue("@OrganizationId", organizationId);
        await using var reader = await cmd.ExecuteReaderAsync();
        var tasks = new List<OrganizationDeleteTask>();
        while (await reader.ReadAsync())
        {
            tasks.Add(new OrganizationDeleteTask
            {
                Id = reader.GetGuid(0),
                OrganizationId = reader.GetGuid(1),
                TaskType = (OrganizationDeleteTaskType)reader.GetByte(2),
                CreationDate = reader.GetDateTime(3),
                RevisionDate = reader.GetDateTime(4),
                StartDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                CompletedDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                ItemsDeletedCount = reader.GetInt64(7),
                FailureCount = reader.GetInt32(8),
                LastError = reader.IsDBNull(9) ? null : reader.GetString(9),
            });
        }
        return tasks;
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
