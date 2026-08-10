#nullable enable

using Bit.Core;
using Bit.Core.Jobs.DataMigrations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Admin.DataMigrations;

/// <summary>
/// Backfills data protection onto OrganizationApiKey.ApiKey (PM-40439 / VULN-679). Enable only
/// after every writer protects on write — rows created mid-migration must already carry the
/// "P|" prefix (they land behind the cursor on any provider). The whole table is one partition:
/// it holds a handful of rows per organization, so a single sequential range converges in a few
/// firings.
/// </summary>
public class OrganizationApiKeyProtectionMigration
    : BaseDataMigration<OrganizationApiKeyMigrationRow, OrganizationApiKeyMigrationUpdate>
{
    private readonly IOrganizationApiKeyMigrationRepository _migrationRepository;
    private readonly IFeatureService _featureService;
    private readonly IDataProtector _dataProtector;

    public OrganizationApiKeyProtectionMigration(
        IOrganizationApiKeyMigrationRepository migrationRepository,
        IFeatureService featureService,
        IDataProtectionProvider dataProtectionProvider,
        IDataMigrationStateRepository stateRepository,
        TimeProvider timeProvider,
        ILogger<OrganizationApiKeyProtectionMigration> logger)
        : base(stateRepository, timeProvider, logger)
    {
        _migrationRepository = migrationRepository;
        _featureService = featureService;
        _dataProtector = dataProtectionProvider.CreateProtector(Constants.DatabaseFieldProtectorPurpose);
    }

    public override string Name => "organization-apikey-protect";

    protected override bool Enabled =>
        _featureService.IsEnabled(FeatureFlagKeys.OrganizationApiKeyProtectionMigration);

    // Examine more rows than one write batch converts: the candidate predicate is evaluated
    // server-side, so already-protected rows cost scan time but never cross the wire. As the
    // migration converges (and on any re-run) firings sweep whole windows of converted rows for
    // the price of the metadata row.
    protected override int ScanWindow => 5 * BatchSize;

    protected override Task<long> CountRowsAsync(CancellationToken token) =>
        _migrationRepository.CountAsync(token);

    protected override async Task<MigrationBatch<OrganizationApiKeyMigrationRow>> ReadBatchAsync(
        string? cursor, string? rangeEnd, CancellationToken token)
    {
        // Single partition (rangeEnd is always null): the table holds a handful of rows per
        // organization, so one sequential range converges in a few firings.
        var from = cursor == null ? Guid.Empty : Guid.Parse(cursor);
        var result = await _migrationRepository.ReadBatchAsync(from, ScanWindow, BatchSize, token);

        // Over-delivery: the window held more candidates than one write batch may take. Checkpoint
        // at the last TAKEN candidate — not the window end — so the next firing re-enters the same
        // window; the cursor never advances past unfinished work.
        var capped = result.CandidateCount > result.Candidates.Count;
        return new MigrationBatch<OrganizationApiKeyMigrationRow>(
            result.Candidates,
            capped
                ? result.Candidates[^1].Id.ToString()
                : result.WindowEnd?.ToString() ?? cursor,
            capped ? result.Candidates.Count : result.ScannedCount,
            EndOfRange: !capped && result.ScannedCount < ScanWindow);
    }

    protected override OrganizationApiKeyMigrationUpdate? Shape(OrganizationApiKeyMigrationRow row) =>
        // Candidates are pre-filtered server-side; this check is a cheap guard, not the filter.
        row.ApiKey.StartsWith(Constants.DatabaseFieldProtectedPrefix)
            ? null
            : new OrganizationApiKeyMigrationUpdate(
                row.Id,
                row.ApiKey,
                string.Concat(Constants.DatabaseFieldProtectedPrefix, _dataProtector.Protect(row.ApiKey)));

    protected override Task<int> WriteBatchAsync(
        IReadOnlyList<OrganizationApiKeyMigrationUpdate> updates, CancellationToken token) =>
        _migrationRepository.ProtectBatchAsync(updates, token);
}
