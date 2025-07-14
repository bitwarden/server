using System.Diagnostics;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class IntegrationConfigurationDetailsCacheService : BackgroundService, IIntegrationConfigurationDetailsCache
{
    private readonly record struct IntegrationCacheKey(Guid OrganizationId, IntegrationType IntegrationType, EventType EventType);
    private readonly IOrganizationIntegrationConfigurationRepository _repository;
    private readonly ILogger<IntegrationConfigurationDetailsCacheService> _logger;
    private readonly TimeSpan _refreshInterval;
    private Dictionary<IntegrationCacheKey, List<OrganizationIntegrationConfigurationDetails>> _cache = new();

    public IntegrationConfigurationDetailsCacheService(
        IOrganizationIntegrationConfigurationRepository repository,
        GlobalSettings globalSettings,
        ILogger<IntegrationConfigurationDetailsCacheService> logger
    )
    {
        _repository = repository;
        _logger = logger;
        _refreshInterval = TimeSpan.FromMinutes(globalSettings.EventLogging.IntegrationCacheRefreshIntervalMinutes);
    }

    public List<OrganizationIntegrationConfigurationDetails> GetConfigurationDetails(
        Guid organizationId,
        IntegrationType integrationType,
        EventType eventType)
    {
        var key = new IntegrationCacheKey(organizationId, integrationType, eventType);
        return _cache.TryGetValue(key, out var value)
            ? value
            : new List<OrganizationIntegrationConfigurationDetails>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAsync();

        var timer = new PeriodicTimer(_refreshInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshAsync();
        }
    }

    private async Task RefreshAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var newCache = (await _repository.GetAllConfigurationDetailsAsync())
                .GroupBy(x => new IntegrationCacheKey(x.OrganizationId, x.IntegrationType, x.EventType))
                .ToDictionary(g => g.Key, g => g.ToList());
            _cache = newCache;

            stopwatch.Stop();
            _logger.LogInformation(
                "[IntegrationConfigurationDetailsCacheService] Refreshed successfully: {Count} entries in {Duration}ms",
                newCache.Count,
                stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError("[IntegrationConfigurationDetailsCacheService] Refresh failed: {ex}", ex);
        }
    }
}
