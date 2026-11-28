using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.Dapper;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Pam.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

/// <summary>
/// The expiry sweep must lock each lease row before its index entries, in the same order as the procedures that
/// revoke or extend a lease. The test holds one due lease the way AccessRequest_CreateApprovedExtension does, then
/// moves its NotAfter while the sweep waits, which deadlocks a sweep that reaches the rows through the index.
/// </summary>
public class AccessLeaseExpiryRaceTests
{
    /// <summary>
    /// Enough due leases that a set-based UPDATE is planned as a seek of the sweep index instead of one primary key
    /// seek per lease.
    /// </summary>
    private const int DueLeaseCount = 400;

    /// <summary>Long enough that a passing run isn't luck, well under the lock wait timeout.</summary>
    private static readonly TimeSpan BlockedGrace = TimeSpan.FromSeconds(2);

    [DatabaseTheory, DatabaseData]
    public async Task ExpireDueAsync_WhileAnExtensionHoldsADueLease_SweepsTheOthersAndLeavesItOut(
        Database database,
        IOrganizationRepository organizationRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        Assert.SkipWhen(database.Type != SupportedDatabaseProviders.SqlServer || database.UseEf,
            "Covers the lock order of PamLeaseExpirySweep_CreateManyDue, which only the Dapper repository runs.");

        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        // A window of its own, so the sweep only finds this test's leases.
        var notAfter = DateTime.UtcNow.Date.AddDays(Random.Shared.Next(36_500, 2_000_000));
        var leaseIds = Enumerable.Range(0, DueLeaseCount).Select(_ => CombGuid.Generate()).ToList();
        await using var setup = new SqlConnection(database.ConnectionString);
        await SeedDueLeasesAsync(setup, organization.Id, leaseIds, notAfter);
        var extendedLeaseId = leaseIds[DueLeaseCount / 2];
        var extendedNotAfter = notAfter.AddHours(1);

        await using var extension = new SqlConnection(database.ConnectionString);
        await extension.OpenAsync();
        await using var transaction = extension.BeginTransaction();
        // Mirrors the lock AccessRequest_CreateApprovedExtension takes before it moves NotAfter.
        await extension.ExecuteAsync(
            "SELECT 1 FROM [dbo].[AccessLease] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = @Id",
            new { Id = extendedLeaseId }, transaction);

        // Act
        var sweep = Task.Run(() => accessLeaseRepository.ExpireDueAsync(notAfter.AddSeconds(1), notAfter.AddMinutes(-1)));
        Assert.True(sweep != await Task.WhenAny(sweep, Task.Delay(BlockedGrace)),
            "The sweep finished while a due lease was held, so it never waited on that lease's row.");

        await extension.ExecuteAsync(
            "UPDATE [dbo].[AccessLease] SET [NotAfter] = @NotAfter WHERE [Id] = @Id",
            new { Id = extendedLeaseId, NotAfter = extendedNotAfter }, transaction);
        await transaction.CommitAsync();
        var swept = await sweep;

        // Assert
        Assert.Equal(leaseIds.Where(id => id != extendedLeaseId).Order(), swept.Select(l => l.Id).Order());
        var extended = await accessLeaseRepository.GetByIdAsync(extendedLeaseId);
        Assert.Null(extended!.SweptDate);
        Assert.Equal(extendedNotAfter, extended.NotAfter);
    }

    /// <summary>
    /// Raw SQL, since creating hundreds of leases through the repository would dominate the test's run time.
    /// Each lease reuses its id for the request it belongs to.
    /// </summary>
    private static Task SeedDueLeasesAsync(SqlConnection connection, Guid organizationId, IEnumerable<Guid> ids,
        DateTime notAfter)
        => connection.ExecuteAsync(
            """
            INSERT INTO [dbo].[AccessRequest]
                ([Id], [OrganizationId], [CollectionId], [CipherId], [RequesterId], [NotBefore], [NotAfter], [Action],
                [CreationDate])
            SELECT [Id], @OrganizationId, [Id], [Id], [Id], @NotBefore, @NotAfter, 1, @NotBefore
            FROM @Ids

            INSERT INTO [dbo].[AccessLease]
                ([Id], [AccessRequestId], [OrganizationId], [CollectionId], [CipherId], [RequesterId], [Action],
                [NotBefore], [NotAfter], [CreationDate])
            SELECT [Id], [Id], @OrganizationId, [Id], [Id], [Id], 0, @NotBefore, @NotAfter, @NotBefore
            FROM @Ids
            """,
            new
            {
                Ids = ids.ToGuidIdArrayTVP(),
                OrganizationId = organizationId,
                NotBefore = notAfter.AddHours(-1),
                NotAfter = notAfter,
            });
}
