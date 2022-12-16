using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationDomains;

public class VerifyOrganizationDomainCommand : IVerifyOrganizationDomainCommand
{
    private readonly IOrganizationDomainRepository _organizationDomainRepository;
    private readonly IDnsResolverService _dnsResolverService;
    private readonly IEventService _eventService;
    private readonly ILogger<VerifyOrganizationDomainCommand> _logger;

    public VerifyOrganizationDomainCommand(
        IOrganizationDomainRepository organizationDomainRepository,
        IDnsResolverService dnsResolverService,
        IEventService eventService,
        ILogger<VerifyOrganizationDomainCommand> logger)
    {
        _organizationDomainRepository = organizationDomainRepository;
        _dnsResolverService = dnsResolverService;
        _eventService = eventService;
        _logger = logger;
    }

    public async Task<OrganizationDomain> VerifyOrganizationDomain(Guid id)
    {
        var domain = await _organizationDomainRepository.GetByIdAsync(id);
        if (domain is null)
        {
            throw new NotFoundException();
        }
        if (domain.VerifiedDate is not null)
        {
            throw new ConflictException("Domain has already been verified.");
        }

        var claimedDomain =
            await _organizationDomainRepository.GetClaimedDomainsByDomainNameAsync(domain.DomainName);
        if (claimedDomain.Any())
        {
            throw new ConflictException("The domain has been verified by another organization.");
        }

        try
        {
            if (await _dnsResolverService.ResolveAsync(domain.DomainName, domain.Txt))
            {
                domain.SetVerifiedDate();
                domain.SetLastCheckedDate();
                await _organizationDomainRepository.ReplaceAsync(domain);
                await _eventService.LogOrganizationDomainEventAsync(domain, EventType.OrganizationDomain_Verified);
            }
        }
        catch (Exception e)
        {
            _logger.LogError("Error verifying Organization domain.", e);
        }

        await _eventService.LogOrganizationDomainEventAsync(domain, EventType.OrganizationDomain_NotVerified);
        return domain;
    }
}
