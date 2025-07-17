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
    private readonly record struct IntegrationCacheKey(Guid OrganizationId, IntegrationType IntegrationType, EventType? EventType);
    private readonly IOrganizationIntegrationConfigurationRepository _repository;
    private readonly ILogger<IntegrationConfigurationDetailsCacheService> _logger;
    private readonly TimeSpan _refreshInterval;
    private Dictionary<IntegrationCacheKey, List<OrganizationIntegrationConfigurationDetails>> _cache = new();

    public IntegrationConfigurationDetailsCacheService(
        IOrganizationIntegrationConfigurationRepository repository,
        GlobalSettings globalSettings,
        ILogger<IntegrationConfigurationDetailsCacheService> logger)
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
        var specificKey = new IntegrationCacheKey(organizationId, integrationType, eventType);
        var allEventsKey = new IntegrationCacheKey(organizationId, integrationType, null);

        var results = new List<OrganizationIntegrationConfigurationDetails>();

        if (_cache.TryGetValue(specificKey, out var specificConfigs))
        {
            results.AddRange(specificConfigs);
        }
        if (_cache.TryGetValue(allEventsKey, out var fallbackConfigs))
        {
            results.AddRange(fallbackConfigs);
        }

        return results;
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

    internal async Task RefreshAsync()
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
