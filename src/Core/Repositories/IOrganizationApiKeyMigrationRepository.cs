#nullable enable

namespace Bit.Core.Repositories;

public record OrganizationApiKeyMigrationRow(Guid Id, string ApiKey);

public record OrganizationApiKeyMigrationUpdate(Guid Id, string OriginalApiKey, string ProtectedApiKey);

/// <summary>
/// The result of one windowed migration read.
/// </summary>
/// <param name="Candidates">Unprotected rows within the window, capped at the batch size.</param>
/// <param name="WindowEnd">Key of the last row examined — the scan high-water mark. Null when the
/// window was empty.</param>
/// <param name="ScannedCount">Rows examined, candidates and non-candidates alike.</param>
/// <param name="CandidateCount">Candidates present in the window, before the batch-size cap —
/// when this exceeds <see cref="Candidates"/>.Count the window over-delivered and the caller must
/// checkpoint at the last taken candidate instead of <see cref="WindowEnd"/>.</param>
public record OrganizationApiKeyMigrationReadResult(
    IReadOnlyList<OrganizationApiKeyMigrationRow> Candidates,
    Guid? WindowEnd,
    int ScannedCount,
    int CandidateCount);

/// <summary>
/// Raw data access for the organization API key protection migration (PM-40439). Reads bypass the
/// regular repository's unprotect path by design — the migration must see stored values verbatim.
/// Writes are conditional per row: SET ApiKey = @Protected WHERE Id = @Id AND ApiKey = @Original,
/// so a concurrent rotation always wins and the migration skips the row.
/// </summary>
public interface IOrganizationApiKeyMigrationRepository
{
    /// <summary>Total rows in the table, read once at first run to anchor the pending-rows
    /// metric and place partition boundaries.</summary>
    Task<long> CountAsync(CancellationToken token);

    /// <summary>
    /// Windowed keyset read: examines exactly the next <paramref name="scanWindow"/> rows after
    /// <paramref name="cursor"/> in database key order (bounded statement cost regardless of
    /// candidate density), returning only the unprotected candidates — at most
    /// <paramref name="batchSize"/> — plus the window's scan metadata.
    /// </summary>
    Task<OrganizationApiKeyMigrationReadResult> ReadBatchAsync(Guid cursor, int scanWindow,
        int batchSize, CancellationToken token);

    /// <summary>Applies the compare-and-swap updates in one transaction, committed on return.
    /// Returns the number of rows actually written.</summary>
    Task<int> ProtectBatchAsync(IReadOnlyList<OrganizationApiKeyMigrationUpdate> updates,
        CancellationToken token);
}
