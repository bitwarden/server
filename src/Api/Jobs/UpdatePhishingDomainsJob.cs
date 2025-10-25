using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Dirt.PhishingDomainFeatures.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Quartz;

namespace Bit.Api.Jobs;

public class UpdatePhishingDomainsJob : BaseJob
{
    private readonly GlobalSettings _globalSettings;
    private readonly IPhishingDomainRepository _phishingDomainRepository;
    private readonly ICloudPhishingDomainQuery _cloudPhishingDomainQuery;
    private readonly IFeatureService _featureService;
    public UpdatePhishingDomainsJob(
        GlobalSettings globalSettings,
        IPhishingDomainRepository phishingDomainRepository,
        ICloudPhishingDomainQuery cloudPhishingDomainQuery,
        IFeatureService featureService,
        ILogger<UpdatePhishingDomainsJob> logger)
        : base(logger)
    {
        _globalSettings = globalSettings;
        _phishingDomainRepository = phishingDomainRepository;
        _cloudPhishingDomainQuery = cloudPhishingDomainQuery;
        _featureService = featureService;
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.PhishingDetection))
        {
            _logger.LogInformation(Constants.BypassFiltersEventId, "Skipping phishing domain update. Feature flag is disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_globalSettings.PhishingDomain?.UpdateUrl))
        {
            _logger.LogInformation(Constants.BypassFiltersEventId, "Skipping phishing domain update. No URL configured.");
            return;
        }

        if (_globalSettings.SelfHosted && !_globalSettings.EnableCloudCommunication)
        {
            _logger.LogInformation(Constants.BypassFiltersEventId, "Skipping phishing domain update. Cloud communication is disabled in global settings.");
            return;
        }

        var remoteChecksum = await _cloudPhishingDomainQuery.GetRemoteChecksumAsync();
        if (string.IsNullOrWhiteSpace(remoteChecksum))
        {
            _logger.LogWarning(Constants.BypassFiltersEventId, "Could not retrieve remote checksum. Skipping update.");
            return;
        }

        var currentChecksum = await _phishingDomainRepository.GetCurrentChecksumAsync();

        if (string.Equals(currentChecksum, remoteChecksum, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(Constants.BypassFiltersEventId,
                "Phishing domains list is up to date (checksum: {Checksum}). Skipping update.",
                currentChecksum);
            return;
        }

        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Checksums differ (current: {CurrentChecksum}, remote: {RemoteChecksum}). Fetching updated domains from {Source}.",
            currentChecksum, remoteChecksum, _globalSettings.SelfHosted ? "Bitwarden cloud API" : "external source");

        try
        {
            var domains = await _cloudPhishingDomainQuery.GetPhishingDomainsAsync();
            if (!domains.Contains("phishing.testcategory.com", StringComparer.OrdinalIgnoreCase))
            {
                domains.Add("phishing.testcategory.com");
            }

            if (domains.Count > 0)
            {
                _logger.LogInformation(Constants.BypassFiltersEventId, "Updating {Count} phishing domains with checksum {Checksum}.",
                    domains.Count, remoteChecksum);
                await _phishingDomainRepository.UpdatePhishingDomainsAsync(domains, remoteChecksum);
                _logger.LogInformation(Constants.BypassFiltersEventId, "Successfully updated phishing domains.");
            }
            else
            {
                _logger.LogWarning(Constants.BypassFiltersEventId, "No valid domains found in the response. Skipping update.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(Constants.BypassFiltersEventId, ex, "Error updating phishing domains.");
        }
    }
}
