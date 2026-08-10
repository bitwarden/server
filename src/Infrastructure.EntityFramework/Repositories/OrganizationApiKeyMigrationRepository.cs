#nullable enable

using AutoMapper;
using Bit.Core;
using Bit.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class OrganizationApiKeyMigrationRepository : BaseEntityFrameworkRepository,
    IOrganizationApiKeyMigrationRepository
{
    public OrganizationApiKeyMigrationRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper)
    { }

    public async Task<long> CountAsync(CancellationToken token)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        return await dbContext.OrganizationApiKeys.LongCountAsync(token);
    }

    public async Task<OrganizationApiKeyMigrationReadResult> ReadBatchAsync(Guid cursor,
        int scanWindow, int batchSize, CancellationToken token)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var prefix = Constants.DatabaseFieldProtectedPrefix;

        // Windowed read, server-evaluated: the window bounds statement cost regardless of
        // candidate density, and the prefix predicate keeps non-candidates off the wire.
        // OrganizationApiKey protection is repository-level (not a value converter), so the mapped
        // property holds the stored value verbatim and both predicates translate cleanly.
        // CompareTo keeps each provider's own Guid ordering consistent between the predicate and
        // the ORDER BY.
        var window = dbContext.OrganizationApiKeys.AsNoTracking()
            .Where(k => k.Id.CompareTo(cursor) > 0)
            .OrderBy(k => k.Id)
            .Take(scanWindow);

        var metadata = await window
            .GroupBy(_ => 1)
            .Select(g => new
            {
                ScannedCount = g.Count(),
                WindowEnd = (Guid?)g.Max(k => k.Id),
                CandidateCount = g.Count(k => !k.ApiKey.StartsWith(prefix)),
            })
            .FirstOrDefaultAsync(token);

        var candidates = await window
            .Where(k => !k.ApiKey.StartsWith(prefix))
            .OrderBy(k => k.Id)
            .Take(batchSize)
            .Select(k => new OrganizationApiKeyMigrationRow(k.Id, k.ApiKey))
            .ToListAsync(token);

        // Two statements, two snapshots: a row inserted between them can skew the counts by one.
        // Metrics-grade impact only — writer sequencing guarantees new rows are already protected,
        // so skipping or re-scanning them is harmless either way.
        return new OrganizationApiKeyMigrationReadResult(
            candidates, metadata?.WindowEnd, metadata?.ScannedCount ?? 0, metadata?.CandidateCount ?? 0);
    }

    public async Task<int> ProtectBatchAsync(IReadOnlyList<OrganizationApiKeyMigrationUpdate> updates,
        CancellationToken token)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Per-row compare-and-swap in one transaction; a concurrently rotated key never matches
        // its OriginalApiKey predicate, so the rotation always wins and the row is skipped.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
        var written = 0;
        foreach (var update in updates)
        {
            written += await dbContext.OrganizationApiKeys
                .Where(k => k.Id == update.Id && k.ApiKey == update.OriginalApiKey)
                .ExecuteUpdateAsync(u => u.SetProperty(k => k.ApiKey, update.ProtectedApiKey), token);
        }
        await transaction.CommitAsync(token);
        return written;
    }
}
