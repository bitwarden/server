using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class OrganizationDomainService : IOrganizationDomainService
{
    private readonly IOrganizationDomainRepository _domainRepository;
    private readonly IDnsResolverService _dnsResolverService;
    private readonly IEventService _eventService;
    private readonly ILogger<OrganizationDomainService> _logger;

    public OrganizationDomainService(
        IOrganizationDomainRepository domainRepository,
        IDnsResolverService dnsResolverService,
        IEventService eventService,
        ILogger<OrganizationDomainService> logger)
    {
        _domainRepository = domainRepository;
        _dnsResolverService = dnsResolverService;
        _eventService = eventService;
        _logger = logger;
    }

    public async Task ValidateOrganizationsDomainAsync()
    {
        //Date should be set 1 hour behind to ensure it selects all domains that should be verified
        var runDate = DateTime.UtcNow.AddHours(-1);

        var verifiableDomains = await _domainRepository.GetManyByNextRunDateAsync(runDate);
        _logger.LogInformation(Constants.BypassFiltersEventId, null,
            "Validating domains for {0} organizations.", verifiableDomains.Count);

        foreach (var domain in verifiableDomains)
        {
            try
            {
                _logger.LogInformation(Constants.BypassFiltersEventId, null,
                    "Attempting verification for {OrgId} with domain {Domain}", domain.OrganizationId, domain.DomainName);

                var status = await _dnsResolverService.ResolveAsync(domain.DomainName, domain.Txt);
                if (status)
                {
                    _logger.LogInformation(Constants.BypassFiltersEventId, "Successfully validated domain");
                    domain.SetVerifiedDate();
                    domain.SetJobRunCount();

                    await _domainRepository.ReplaceAsync(domain);
                    await _eventService.LogOrganizationDomainEventAsync(domain, EventType.OrganizationDomain_Verified,
                        EventSystemUser.SSO);
                    return;
                }

                domain.SetJobRunCount();
                domain.SetNextRunDate();
                await _domainRepository.ReplaceAsync(domain);
                await _eventService.LogOrganizationDomainEventAsync(domain, EventType.OrganizationDomain_NotVerified,
                    EventSystemUser.SSO);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Verification for organization {OrgId} with domain {Domain} failed", domain.OrganizationId, domain.DomainName);
            }
        }
    }

    public async Task OrganizationDomainMaintenanceAsync()
    {
        //Get domains that have not been verified within 72 hours
        //Send email to administrators
        //Update table with email sent

        //check domains that have not been verified within 7 days 
        //delete domains

        //end
    }
}
