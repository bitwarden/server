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
/// The activation/retraction write skew and the claim that closes it: activation used to write only AccessLease
/// while retraction wrote only AccessRequest, so both could commit and leave a cancelled request holding a live
/// lease. Both sides now claim the request row first.
/// </summary>
/// <remarks>
/// Not a <c>Task.WhenAll</c> race, since the losing interleaving is rare and plan-dependent. Each test instead
/// holds the request row in its own uncommitted transaction and asserts the real counterparty blocks on it.
/// </remarks>
public class AccessRequestActivationRaceTests
{
    /// <summary>
    /// Grace period given to the blocked counterparty before the held row is released: long enough that a
    /// passing run isn't luck, well under every provider's lock wait timeout.
    /// </summary>
    /// <remarks>
    /// Held this long deliberately; shortening it would let a merely slow run read as "blocked" for the wrong reason.
    /// </remarks>
    private static readonly TimeSpan BlockedGrace = TimeSpan.FromSeconds(2);

    // Activation's claim: a retraction that reached the row first must block the mint, which then fails its CAS.
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

        // Activation must not insert a lease while the row is held.
        Assert.True(mint != await Task.WhenAny(mint, Task.Delay(BlockedGrace)),
            "Activation ran to completion while the request row was held, so it never claimed the row. Without that " +
            "claim its precondition read is an ordinary MVCC read that sees a pre-retraction state and mints anyway.");

        // Settle the retraction the way AccessRequest_Cancel does, and let go.
        await held.CancelAsync(now);
        await held.CommitAsync();

        // PostgreSQL surfaces this as a 40001; the retry yields the same outcome on every provider.
        Assert.Equal(AccessLeaseMintOutcome.PreconditionFailed, await mint);

        Assert.Null(await accessLeaseRepository.GetByAccessRequestIdAsync(request.Id));
        var settled = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.Equal(AccessRequestAction.Cancelled, settled!.Action);
    }

    // The retraction's claim: without it, the lease probe can run before AccessRequest is locked, missing a
    // concurrent mint.
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

        // Stands in for an activation mid-flight: row claimed and lease minted, but not yet committed.
        await using var held = await HeldRequestRow.ClaimAsync(database, request.Id);
        var lease = BuildLeaseFor(request, now);
        await held.InsertLeaseAsync(lease, now);

        var cancel = Task.Run(() => accessRequestRepository.CancelAsync(request.Id, now));

        // Without the claim, the retraction could read past the held row and complete.
        Assert.True(cancel != await Task.WhenAny(cancel, Task.Delay(BlockedGrace)),
            "The retraction ran to completion while the request row was held, so it never claimed the row and its " +
            "lease probe was free to run before the row was locked.");

        await held.CommitAsync();
        await cancel;

        // Activation won: the request stays Approved and keeps its lease.
        var settled = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.Equal(AccessRequestAction.Approved, settled!.Action);

        var minted = await accessLeaseRepository.GetByAccessRequestIdAsync(request.Id);
        Assert.NotNull(minted);
        Assert.Equal(lease.Id, minted.Id);
        Assert.Equal(AccessLeaseAction.None, minted.Action);
    }

    // Two concurrent activations of the same request: the unique index on AccessLease, not the CAS on
    // AccessRequest.Action, is what refuses the second lease.
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

        // One request, one lease: the unique index refuses a second grant over the same window.
        var minted = await accessLeaseRepository.GetByAccessRequestIdAsync(request.Id);
        Assert.NotNull(minted);
        Assert.Equal(winner.Id, minted.Id);
    }

    private const string SqliteSkipReason =
        "SQLite serializes every writer at the database level, so the two-table write skew cannot occur there. It " +
        "also cannot express the setup: a second connection's write against a held transaction fails outright with " +
        "SQLITE_BUSY instead of waiting on the row.";

    /// <summary>
    /// Holds the AccessRequest row's write lock on its own uncommitted transaction, so a test can assert a real
    /// counterparty blocks on it, then release it and check how the counterparty settles.
    /// </summary>
    /// <remarks>
    /// Raw SQL, not a repository, because it must keep one transaction open across the counterparty's whole run.
    /// </remarks>
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

        /// <summary>Settles the held request as a requester cancellation, mirroring AccessRequest_Cancel's UPDATE.</summary>
        public Task CancelAsync(DateTime now)
            => ExecuteAsync(
                $"UPDATE {Table("AccessRequest")} SET {Name("Action")} = 3, {Name("ActionDate")} = @Now " +
                $"WHERE {Name("Id")} = @Id",
                ("@Now", now));

        /// <summary>Mints the lease the held claim is standing in for, still inside the uncommitted transaction.</summary>
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
