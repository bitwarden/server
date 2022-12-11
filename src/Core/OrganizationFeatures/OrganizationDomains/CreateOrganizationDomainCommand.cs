using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationDomains;

public class CreateOrganizationDomainCommand : ICreateOrganizationDomainCommand
{
    private readonly IOrganizationDomainRepository _organizationDomainRepository;
    private readonly IEventService _eventService;
    private readonly IDnsResolverService _dnsResolverService;
    private readonly ILogger<VerifyOrganizationDomainCommand> _logger;

    public CreateOrganizationDomainCommand(
        IOrganizationDomainRepository organizationDomainRepository,
        IEventService eventService,
        IDnsResolverService dnsResolverService,
        ILogger<VerifyOrganizationDomainCommand> logger)
    {
        _organizationDomainRepository = organizationDomainRepository;
        _eventService = eventService;
        _dnsResolverService = dnsResolverService;
        _logger = logger;
    }

    public async Task<OrganizationDomain> CreateAsync(OrganizationDomain organizationDomain)
    {
        var verifiedStatus = false;
        //Domains claimed and verified by an organization cannot be claimed
        var claimedDomain =
            await _organizationDomainRepository.GetClaimedDomainsByDomainNameAsync(organizationDomain.DomainName);
        if (claimedDomain.Any())
        {
            throw new DomainClaimedException();
        }

        //check for duplicate domain entry for an organization
        var duplicateOrgDomain =
            await _organizationDomainRepository.GetDomainByOrganizationIdAsync(organizationDomain.OrganizationId,
                organizationDomain.DomainName);
        if (duplicateOrgDomain is not null)
        {
            throw new DuplicateDomainException();
        }

        try
        {
            if (await _dnsResolverService.ResolveAsync(organizationDomain.DomainName, organizationDomain.Txt))
            {
                organizationDomain.SetVerifiedDate();
                verifiedStatus = true;
            }
        }
        catch (Exception e)
        {
            _logger.LogError("Error verifying Organization domain.", e);
        }

        organizationDomain.SetNextRunDate();
        organizationDomain.SetLastCheckedDate();

        var orgDomain = await _organizationDomainRepository.CreateAsync(organizationDomain);

        await _eventService.LogOrganizationDomainEventAsync(orgDomain, EventType.OrganizationDomain_Added);
        await _eventService.LogOrganizationDomainEventAsync(orgDomain,
            verifiedStatus ? EventType.OrganizationDomain_Verified : EventType.OrganizationDomain_NotVerified);

        return orgDomain;
    }
}
