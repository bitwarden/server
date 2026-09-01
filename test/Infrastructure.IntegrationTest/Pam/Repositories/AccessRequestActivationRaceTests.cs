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
/// The activation/retraction write skew (PM-41878) and the claim that closes it.
///
/// Activation used to read AccessRequest and write only AccessLease, while a retraction (requester cancel, or a
/// manager's cancel-with-decision) reads AccessLease and writes only AccessRequest. Neither side wrote the table the
/// other read, so both could commit and leave a Cancelled/Denied request holding a live lease -- and access is
/// governed by the lease alone once it exists, so that combination hands the requester the credential their request
/// was withdrawn from. Both sides now claim the request row first, which is what these tests pin.
/// </summary>
/// <remarks>
/// Deliberately not a <c>Task.WhenAll</c> race. Firing both operations at once and asserting the invariant would pass
/// whether or not either claim exists, because the losing interleaving is rare and plan-dependent. Instead each test
/// holds the request row in an uncommitted transaction of its own and asserts that the real counterparty
/// <em>blocks</em> on it -- the mechanism, not the symptom.
///
/// Against the pre-fix repositories on PostgreSQL both fail, and the second fails by reproducing the defect itself:
/// the retraction settles Cancelled on a request that is holding a live lease. (On SQL Server the pre-fix procedures
/// happened to produce a plan that ordered the two correctly, so there these pin the invariant rather than
/// demonstrate the fault -- which is the point of fixing it by construction instead of by plan.)
/// </remarks>
public class AccessRequestActivationRaceTests
{
    /// <summary>
    /// How long the blocked counterparty is given to (wrongly) run to completion before the held transaction is
    /// released. Long enough that a passing run is not luck, short enough not to drag the suite; every provider's
    /// lock wait timeout is far longer (SQL Server and PostgreSQL wait indefinitely by default, MySQL for 50s).
    /// </summary>
    /// <remarks>
    /// Held this long deliberately, at a known cost: an open transaction of this age holds PostgreSQL's transaction
    /// horizon back, which under the suite's parallelism makes an unrelated Serializable writer marginally likelier
    /// to be aborted with 40001 -- measured at roughly one occurrence per nine full runs of Pam.Repositories, in
    /// CreateApprovedExtensionAsync, which does not retry. Shortening this would trade a firm assertion for a
    /// cosmetic one: a counterparty that merely ran slowly would read as "blocked" and the test would pass for the
    /// wrong reason.
    /// </remarks>
    private static readonly TimeSpan BlockedGrace = TimeSpan.FromSeconds(2);

    // Activation's half of the claim: a retraction that reached the request row first must make the mint wait, and
    // once it commits the mint has to lose its CAS on Action rather than mint over a request that is now Cancelled.
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

        // The claim is the whole point: activation must not get as far as inserting a lease while the row is held.
        Assert.True(mint != await Task.WhenAny(mint, Task.Delay(BlockedGrace)),
            "Activation ran to completion while the request row was held, so it never claimed the row. Without that " +
            "claim its precondition read is an ordinary MVCC read that sees a pre-retraction state and mints anyway.");

        // Settle the retraction the way AccessRequest_Cancel does, and let go.
        await held.CancelAsync(now);
        await held.CommitAsync();

        // PostgreSQL surfaces the released block to a Serializable transaction as a 40001 rather than re-qualifying
        // it in place; CreateFromApprovedRequestAsync's retry runs the whole attempt again on a fresh snapshot and
        // arrives at the same answer, so the outcome code is the same on every provider.
        Assert.Equal(AccessLeaseMintOutcome.PreconditionFailed, await mint);

        Assert.Null(await accessLeaseRepository.GetByAccessRequestIdAsync(request.Id));
        var settled = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.Equal(AccessRequestAction.Cancelled, settled!.Action);
    }

    // The retraction's half of the claim, and the one the optimizer could previously get wrong: the lease probe is
    // correlated to the request id rather than to the row being updated, so without the claim it can be evaluated
    // before AccessRequest is locked at all -- and a mint that commits in that gap goes unseen.
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

        // Stand in for an activation mid-flight: it has claimed the request row and minted, but not yet committed.
        // The real mint cannot be used here because it commits before returning, and the whole question is what a
        // retraction does while the lease is still invisible to it.
        await using var held = await HeldRequestRow.ClaimAsync(database, request.Id);
        var lease = BuildLeaseFor(request, now);
        await held.InsertLeaseAsync(lease, now);

        var cancel = Task.Run(() => accessRequestRepository.CancelAsync(request.Id, now));

        // Without the claim the retraction can read straight past the held row into an AccessLease table that still
        // looks empty, and complete.
        Assert.True(cancel != await Task.WhenAny(cancel, Task.Delay(BlockedGrace)),
            "The retraction ran to completion while the request row was held, so it never claimed the row and its " +
            "lease probe was free to run before the row was locked.");

        await held.CommitAsync();
        await cancel;

        // The activation won, so the request stays Approved and keeps the lease that governs it. A Cancelled request
        // here would be the bug: withdrawn on paper, still holding live access to the credential.
        var settled = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.Equal(AccessRequestAction.Approved, settled!.Action);

        var minted = await accessLeaseRepository.GetByAccessRequestIdAsync(request.Id);
        Assert.NotNull(minted);
        Assert.Equal(lease.Id, minted.Id);
        Assert.Equal(AccessLeaseAction.None, minted.Action);
    }

    // Two activations of the same request, with no singleton guard asked for -- the path that now runs at
    // ReadCommitted rather than Serializable. Nothing about the request's state changes when it is activated, so the
    // loser's CAS on Action can still pass on a stale snapshot after it unblocks; the unique
    // IX_AccessLease_AccessRequestId is what actually refuses it, and this is the test that says so.
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

        // One request, one lease -- the first one. A second lease here would be a duplicate grant over the same
        // window, which is what the unique index exists to prevent.
        var minted = await accessLeaseRepository.GetByAccessRequestIdAsync(request.Id);
        Assert.NotNull(minted);
        Assert.Equal(winner.Id, minted.Id);
    }

    private const string SqliteSkipReason =
        "SQLite serializes every writer at the database level, so the two-table write skew cannot occur there. It " +
        "also cannot express the setup: a second connection's write against a held transaction fails outright with " +
        "SQLITE_BUSY instead of waiting on the row.";

    /// <summary>
    /// An uncommitted transaction on its own connection holding the AccessRequest row's write lock -- the same claim
    /// activation and retraction each take before they touch AccessLease. Lets a test assert that the real
    /// counterparty blocks on that row, then release it and check how the counterparty settles.
    /// </summary>
    /// <remarks>
    /// Raw SQL rather than a repository or a DbContext, because it has to hold one transaction open across the
    /// counterparty's whole run and the repositories all own (and commit) their own. The statements are trivial and
    /// portable; only identifier quoting differs by provider.
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

            // The claim itself: a write that changes nothing but takes the row. Exactly what
            // AccessLease_CreateFromApprovedRequest and AccessRequestRepository.ClaimRequestRowAsync do.
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
            // A test that failed its blocking assertion leaves the row held; rolling back keeps the failure readable
            // instead of hanging the rest of the run behind an abandoned lock.
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
