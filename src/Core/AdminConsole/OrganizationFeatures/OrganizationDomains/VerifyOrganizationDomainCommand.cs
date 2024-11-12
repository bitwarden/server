using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;

public class VerifyOrganizationDomainCommand : IVerifyOrganizationDomainCommand
{
    private readonly IOrganizationDomainRepository _organizationDomainRepository;
    private readonly IDnsResolverService _dnsResolverService;
    private readonly IEventService _eventService;
    private readonly IGlobalSettings _globalSettings;
    private readonly IPolicyService _policyService;
    private readonly IFeatureService _featureService;
    private readonly ILogger<VerifyOrganizationDomainCommand> _logger;

    public VerifyOrganizationDomainCommand(
        IOrganizationDomainRepository organizationDomainRepository,
        IDnsResolverService dnsResolverService,
        IEventService eventService,
        IGlobalSettings globalSettings,
        IPolicyService policyService,
        IFeatureService featureService,
        ILogger<VerifyOrganizationDomainCommand> logger)
    {
        _organizationDomainRepository = organizationDomainRepository;
        _dnsResolverService = dnsResolverService;
        _eventService = eventService;
        _globalSettings = globalSettings;
        _policyService = policyService;
        _featureService = featureService;
        _logger = logger;
    }


    public async Task<OrganizationDomain> UserVerifyOrganizationDomainAsync(OrganizationDomain organizationDomain)
    {
        var domainVerificationResult = await VerifyOrganizationDomainAsync(organizationDomain);

        await _eventService.LogOrganizationDomainEventAsync(domainVerificationResult,
            domainVerificationResult.VerifiedDate != null
                ? EventType.OrganizationDomain_Verified
                : EventType.OrganizationDomain_NotVerified);

        await _organizationDomainRepository.ReplaceAsync(domainVerificationResult);

        return domainVerificationResult;
    }

    public async Task<OrganizationDomain> SystemVerifyOrganizationDomainAsync(OrganizationDomain organizationDomain)
    {
        organizationDomain.SetJobRunCount();

        var domainVerificationResult = await VerifyOrganizationDomainAsync(organizationDomain);

        if (domainVerificationResult.VerifiedDate is not null)
        {
            _logger.LogInformation(Constants.BypassFiltersEventId, "Successfully validated domain");

            await _eventService.LogOrganizationDomainEventAsync(domainVerificationResult,
                EventType.OrganizationDomain_Verified,
                EventSystemUser.DomainVerification);
        }
        else
        {
            domainVerificationResult.SetNextRunDate(_globalSettings.DomainVerification.VerificationInterval);

            await _eventService.LogOrganizationDomainEventAsync(domainVerificationResult,
                EventType.OrganizationDomain_NotVerified,
                EventSystemUser.DomainVerification);

            _logger.LogInformation(Constants.BypassFiltersEventId,
                "Verification for organization {OrgId} with domain {Domain} failed",
                domainVerificationResult.OrganizationId, domainVerificationResult.DomainName);
        }

        await _organizationDomainRepository.ReplaceAsync(domainVerificationResult);

        return domainVerificationResult;
    }

    private async Task<OrganizationDomain> VerifyOrganizationDomainAsync(OrganizationDomain domain)
    {
        domain.SetLastCheckedDate();

        if (domain.VerifiedDate is not null)
        {
            await _organizationDomainRepository.ReplaceAsync(domain);
            throw new ConflictException("Domain has already been verified.");
        }

        var claimedDomain =
            await _organizationDomainRepository.GetClaimedDomainsByDomainNameAsync(domain.DomainName);

        if (claimedDomain.Count > 0)
        {
            await _organizationDomainRepository.ReplaceAsync(domain);
            throw new ConflictException("The domain is not available to be claimed.");
        }

        try
        {
            if (await _dnsResolverService.ResolveAsync(domain.DomainName, domain.Txt))
            {
                domain.SetVerifiedDate();

                await EnableSingleOrganizationPolicyAsync(domain.OrganizationId);
            }
        }
        catch (Exception e)
        {
            _logger.LogError("Error verifying Organization domain: {domain}. {errorMessage}",
                domain.DomainName, e.Message);
        }

        return domain;
    }

    private async Task EnableSingleOrganizationPolicyAsync(Guid organizationId)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning))
        {
            await _policyService.SaveAsync(
                new Policy { OrganizationId = organizationId, Type = PolicyType.SingleOrg, Enabled = true }, null);
        }
    }
}
