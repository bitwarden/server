using Bit.Core.Enums;
using Bit.Core.Jobs;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Quartz;

namespace Bit.Api.Jobs;

public class SelfHostedSponsorshipSyncJob : BaseJob
{
    private readonly IServiceProvider _serviceProvider;
    private IOrganizationRepository _organizationRepository;
    private IOrganizationConnectionRepository _organizationConnectionRepository;
    private readonly ILicensingService _licensingService;
    private GlobalSettings _globalSettings;

    public SelfHostedSponsorshipSyncJob(
        IServiceProvider serviceProvider,
        IOrganizationRepository organizationRepository,
        IOrganizationConnectionRepository organizationConnectionRepository,
        ILicensingService licensingService,
        ILogger<SelfHostedSponsorshipSyncJob> logger,
        GlobalSettings globalSettings)
        : base(logger)
    {
        _serviceProvider = serviceProvider;
        _organizationRepository = organizationRepository;
        _organizationConnectionRepository = organizationConnectionRepository;
        _licensingService = licensingService;
        _globalSettings = globalSettings;
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        if (!_globalSettings.EnableCloudCommunication)
        {
            _logger.LogInformation("Skipping Organization sync with cloud - Cloud communication is disabled in global settings");
            return;
        }

        var organizations = await _organizationRepository.GetManyByEnabledAsync();

        using (var scope = _serviceProvider.CreateScope())
        {
            var syncCommand = scope.ServiceProvider.GetRequiredService<ISelfHostedSyncSponsorshipsCommand>();
            foreach (var org in organizations)
            {
                var connection = (await _organizationConnectionRepository.GetEnabledByOrganizationIdTypeAsync(org.Id, OrganizationConnectionType.CloudBillingSync)).FirstOrDefault();
                if (connection != null)
                {
                    try
                    {
                        var config = connection.GetConfig<BillingSyncConfig>();
                        await syncCommand.SyncOrganization(org.Id, config.CloudOrganizationId, connection);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Sponsorship sync for organization {org.Name} Failed");
                    }
                }
            }
        }
    }
}
