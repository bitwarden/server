using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class VerificationDomainService : IVerificationDomainService
{
    private readonly IOrganizationDomainRepository _domainRepository;
    private readonly IDnsResolverService _dnsResolverService;
    private readonly ILogger<VerificationDomainService> _logger;

    public VerificationDomainService(
        IOrganizationDomainRepository domainRepository,
        IDnsResolverService dnsResolverService,
        ILogger<VerificationDomainService> logger)
    {
        _domainRepository = domainRepository;
        _dnsResolverService = dnsResolverService;
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

                    await _domainRepository.ReplaceAsync(domain);
                    return;
                }

                domain.SetJobRunCount();
                domain.SetNextRunDate();
                await _domainRepository.ReplaceAsync(domain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Verification for organization {OrgId} with domain {Domain} failed", domain.OrganizationId, domain.DomainName);
            }
        }
    }
}
