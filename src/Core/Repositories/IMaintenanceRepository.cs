namespace Bit.Core.Repositories;

public interface IMaintenanceRepository
{
    Task UpdateStatisticsAsync();
    Task DisableCipherAutoStatsAsync();
    Task RebuildIndexesAsync();
    Task DeleteExpiredGrantsAsync();
    Task DeleteExpiredSponsorshipsAsync(DateTime validUntilBeforeDate);
}
