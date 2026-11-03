using System.Data.Common;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

/// <summary>
/// Activation and retraction both claim the AccessRequest row first, so a cancelled request never keeps a live lease.
/// Each test holds the row in an uncommitted transaction, since a <c>Task.WhenAll</c> race rarely hits the losing
/// interleaving.
/// </summary>
public class AccessRequestActivationRaceTests
{
    /// <summary>Long enough that a passing run isn't luck, well under every provider's lock wait timeout.</summary>
    private static readonly TimeSpan BlockedGrace = TimeSpan.FromSeconds(2);

    [DatabaseTheory, DatabaseData]
    public async Task CreateFromApprovedRequestAsync_WhileARetractionHoldsTheRequestRow_BlocksThenFailsPrecondition(
        Database database,
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        Assert.SkipWhen(database.Type == SupportedDatabaseProviders.Sqlite, SqliteSkipReason);

        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var request = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-1), now.AddHours(1));

        await using var held = await HeldRequestRow.ClaimAsync(database, request.Id);

        var mint = Task.Run(() =>
            accessLeaseRepository.CreateFromApprovedRequestAsync(BuildLeaseFor(request, now), now, false));

        Assert.True(mint != await Task.WhenAny(mint, Task.Delay(BlockedGrace)),
            "Activation ran to completion while the request row was held, so it never claimed the row. Without that " +
            "claim its precondition read is an ordinary MVCC read that sees a pre-retraction state and mints anyway.");

        await held.CancelAsync(now);
        await held.CommitAsync();

        // PostgreSQL surfaces this as a 40001; the retry yields the same outcome on every provider.
        Assert.Equal(AccessLeaseMintOutcome.PreconditionFailed, await mint);

        Assert.Null(await accessLeaseRepository.GetByAccessRequestIdAsync(request.Id));
        var settled = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.Equal(AccessRequestAction.Cancelled, settled!.Action);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CancelAsync_WhileAnActivationHoldsTheRequestRow_BlocksThenRefusesTheMintedLease(
        Database database,
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        Assert.SkipWhen(database.Type == SupportedDatabaseProviders.Sqlite, SqliteSkipReason);

        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var request = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-1), now.AddHours(1));

        // Simulates an activation mid-flight, with the row claimed and the lease minted but not committed.
        await using var held = await HeldRequestRow.ClaimAsync(database, request.Id);
        var lease = BuildLeaseFor(request, now);
        await held.InsertLeaseAsync(lease, now);

        var cancel = Task.Run(() => accessRequestRepository.CancelAsync(request.Id, now));

        Assert.True(cancel != await Task.WhenAny(cancel, Task.Delay(BlockedGrace)),
            "The retraction ran to completion while the request row was held, so it never claimed the row and its " +
            "lease probe was free to run before the row was locked.");

        await held.CommitAsync();
        await cancel;

        var settled = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.Equal(AccessRequestAction.Approved, settled!.Action);

        var minted = await accessLeaseRepository.GetByAccessRequestIdAsync(request.Id);
        Assert.NotNull(minted);
        Assert.Equal(lease.Id, minted.Id);
        Assert.Equal(AccessLeaseAction.None, minted.Action);
    }

    // Activation leaves Action at Approved, so the CAS cannot refuse the second lease; the existing-lease check or
    // the unique index on AccessLease has to.
    [DatabaseTheory, DatabaseData]
    public async Task CreateFromApprovedRequestAsync_WhileAnotherActivationHoldsTheRequestRow_RefusesTheSecondLease(
        Database database,
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        Assert.SkipWhen(database.Type == SupportedDatabaseProviders.Sqlite, SqliteSkipReason);

        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var request = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-1), now.AddHours(1));

        await using var held = await HeldRequestRow.ClaimAsync(database, request.Id);
        var winner = BuildLeaseFor(request, now);
        await held.InsertLeaseAsync(winner, now);

        var loser = BuildLeaseFor(request, now);
        var mint = Task.Run(() => accessLeaseRepository.CreateFromApprovedRequestAsync(loser, now, false));

        Assert.True(mint != await Task.WhenAny(mint, Task.Delay(BlockedGrace)),
            "The second activation ran to completion while the request row was held, so it never claimed the row.");

        await held.CommitAsync();

        Assert.Equal(AccessLeaseMintOutcome.PreconditionFailed, await mint);

        var minted = await accessLeaseRepository.GetByAccessRequestIdAsync(request.Id);
        Assert.NotNull(minted);
        Assert.Equal(winner.Id, minted.Id);
    }

    private const string SqliteSkipReason =
        "SQLite serializes every writer at the database level, so the two-table write skew cannot occur there. It " +
        "also cannot express the setup: a second connection's write against a held transaction fails outright with " +
        "SQLITE_BUSY instead of waiting on the row.";

    /// <summary>
    /// Holds the AccessRequest row's write lock in an uncommitted transaction. Raw SQL, since no repository keeps one
    /// transaction open across the counterparty's run.
    /// </summary>
    private sealed class HeldRequestRow : IAsyncDisposable
    {
        private readonly SupportedDatabaseProviders _provider;
        private readonly DbConnection _connection;
        private readonly DbTransaction _transaction;
        private readonly Guid _requestId;
        private bool _committed;

        private HeldRequestRow(SupportedDatabaseProviders provider, DbConnection connection,
            DbTransaction transaction, Guid requestId)
        {
            _provider = provider;
            _connection = connection;
            _transaction = transaction;
            _requestId = requestId;
        }

        public static async Task<HeldRequestRow> ClaimAsync(Database database, Guid requestId)
        {
            var connection = Connect(database);
            await connection.OpenAsync();
            var transaction = await connection.BeginTransactionAsync();
            var held = new HeldRequestRow(database.Type, connection, transaction, requestId);

            // Mirrors the no-op UPDATE that ClaimRequestRowAsync uses to take the row.
            await held.ExecuteAsync(
                $"UPDATE {held.Table("AccessRequest")} SET {held.Name("Action")} = {held.Name("Action")} " +
                $"WHERE {held.Name("Id")} = @Id");
            return held;
        }

        /// <summary>Mirrors the UPDATE in AccessRequest_UpdateCancelled.</summary>
        public Task CancelAsync(DateTime now)
            => ExecuteAsync(
                $"UPDATE {Table("AccessRequest")} SET {Name("Action")} = 3, {Name("ActionDate")} = @Now " +
                $"WHERE {Name("Id")} = @Id",
                ("@Now", now));

        /// <summary>Mints the lease inside the held, uncommitted transaction.</summary>
        public Task InsertLeaseAsync(AccessLease lease, DateTime now)
            => ExecuteAsync(
                $"INSERT INTO {Table("AccessLease")} (" +
                $"{Name("Id")}, {Name("AccessRequestId")}, {Name("OrganizationId")}, {Name("CollectionId")}, " +
                $"{Name("CipherId")}, {Name("RequesterId")}, {Name("Action")}, {Name("NotBefore")}, " +
                $"{Name("NotAfter")}, {Name("RevokedDate")}, {Name("RevokedBy")}, {Name("CreationDate")}) " +
                "VALUES (@LeaseId, @Id, @OrganizationId, @CollectionId, @CipherId, @RequesterId, 0, @NotBefore, " +
                "@NotAfter, NULL, NULL, @Now)",
                ("@LeaseId", lease.Id),
                ("@OrganizationId", lease.OrganizationId),
                ("@CollectionId", lease.CollectionId),
                ("@CipherId", lease.CipherId),
                ("@RequesterId", lease.RequesterId),
                ("@NotBefore", lease.NotBefore),
                ("@NotAfter", lease.NotAfter),
                ("@Now", now));

        public async Task CommitAsync()
        {
            await _transaction.CommitAsync();
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            // Rolls back on failure so an abandoned lock doesn't hang the rest of the run.
            if (!_committed)
            {
                await _transaction.RollbackAsync();
            }

            await _transaction.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
        {
            await using var command = _connection.CreateCommand();
            command.Transaction = _transaction;
            command.CommandText = sql;
            command.AddParameter("@Id", _requestId);
            foreach (var (name, value) in parameters)
            {
                command.AddParameter(name, value);
            }

            await command.ExecuteNonQueryAsync();
        }

        private string Table(string name)
            => _provider == SupportedDatabaseProviders.SqlServer ? $"[dbo].[{name}]" : Name(name);

        private string Name(string name) => _provider switch
        {
            SupportedDatabaseProviders.SqlServer => $"[{name}]",
            SupportedDatabaseProviders.MySql => $"`{name}`",
            _ => $"\"{name}\"",
        };

        private static DbConnection Connect(Database database) => database.Type switch
        {
            SupportedDatabaseProviders.SqlServer => new SqlConnection(database.ConnectionString),
            SupportedDatabaseProviders.Postgres => new NpgsqlConnection(database.ConnectionString),
            SupportedDatabaseProviders.MySql => new MySqlConnection(database.ConnectionString),
            SupportedDatabaseProviders.Sqlite => new SqliteConnection(database.ConnectionString),
            _ => throw new NotSupportedException($"No connection for {database.Type}."),
        };
    }

    private static async Task<AccessRequest> CreateApprovedRequestAsync(
        IAccessRequestRepository accessRequestRepository, Guid organizationId, DateTime notBefore, DateTime notAfter)
        => await accessRequestRepository.CreateAsync(new AccessRequest
        {
            OrganizationId = organizationId,
            CollectionId = Guid.NewGuid(),
            CipherId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            NotBefore = notBefore,
            NotAfter = notAfter,
            Reason = "audit",
            Action = AccessRequestAction.Approved,
            CreationDate = DateTime.UtcNow,
            ActionDate = DateTime.UtcNow,
        });

    private static AccessLease BuildLeaseFor(AccessRequest request, DateTime now)
        => new()
        {
            Id = CombGuid.Generate(),
            AccessRequestId = request.Id,
            OrganizationId = request.OrganizationId,
            CollectionId = request.CollectionId,
            CipherId = request.CipherId,
            RequesterId = request.RequesterId,
            Action = AccessLeaseAction.None,
            NotBefore = request.NotBefore,
            NotAfter = request.NotAfter,
            CreationDate = now,
        };
}

internal static class DbCommandParameterExtensions
{
    public static void AddParameter(this DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
