using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.PhishingDomainFeatures.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Quartz;

namespace Bit.Api.Jobs;

public class UpdatePhishingDomainsJob : BaseJob
{
    private readonly GlobalSettings _globalSettings;
    private readonly IPhishingDomainRepository _phishingDomainRepository;
    private readonly ICloudPhishingDomainQuery _cloudPhishingDomainQuery;

    public UpdatePhishingDomainsJob(
        GlobalSettings globalSettings,
        IPhishingDomainRepository phishingDomainRepository,
        ICloudPhishingDomainQuery cloudPhishingDomainQuery,
        ILogger<UpdatePhishingDomainsJob> logger)
        : base(logger)
    {
        _globalSettings = globalSettings;
        _phishingDomainRepository = phishingDomainRepository;
        _cloudPhishingDomainQuery = cloudPhishingDomainQuery;
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
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

        _logger.LogInformation(Constants.BypassFiltersEventId, "Fetching phishing domains from {Source}.", 
            _globalSettings.SelfHosted ? "Bitwarden cloud API" : "external source");
        
        try
        {
            var domains = await _cloudPhishingDomainQuery.GetPhishingDomainsAsync();
            
            if (domains.Count > 0)
            {
                _logger.LogInformation(Constants.BypassFiltersEventId, "Updating {Count} phishing domains.", domains.Count);
                await _phishingDomainRepository.UpdatePhishingDomainsAsync(domains);
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