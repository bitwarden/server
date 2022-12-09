using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationDomains;

public class CreateOrganizationDomainCommand : ICreateOrganizationDomainCommand
{
    private readonly IOrganizationDomainRepository _organizationDomainRepository;
    private readonly IEventService _eventService;

    public CreateOrganizationDomainCommand(IOrganizationDomainRepository organizationDomainRepository,
        IEventService eventService)
    {
        _organizationDomainRepository = organizationDomainRepository;
        _eventService = eventService;
    }

    public async Task<OrganizationDomain> CreateAsync(OrganizationDomain organizationDomain)
    {
        //Domains claimed and verified by an organization cannot be claimed
        var claimedDomain =
            await _organizationDomainRepository.GetClaimedDomainsByDomainNameAsync(organizationDomain.DomainName);
        if (claimedDomain.Any())
        {
            throw new DomainClaimedException();
        }

        organizationDomain.SetNextRunDate();
        
        var orgDomain = await _organizationDomainRepository.CreateAsync(organizationDomain);
        await _eventService.LogOrganizationDomainEventAsync(orgDomain, EventType.OrganizationDomain_Added);
        return orgDomain;
    }
}
