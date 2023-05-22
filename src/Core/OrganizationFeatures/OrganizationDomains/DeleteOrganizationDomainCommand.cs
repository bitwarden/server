using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationDomains;

public class DeleteOrganizationDomainCommand : IDeleteOrganizationDomainCommand
{
    private readonly IOrganizationDomainRepository _organizationDomainRepository;
    private readonly IEventService _eventService;

    public DeleteOrganizationDomainCommand(IOrganizationDomainRepository organizationDomainRepository,
        IEventService eventService)
    {
        _organizationDomainRepository = organizationDomainRepository;
        _eventService = eventService;
    }

    public async Task DeleteAsync(Guid id)
    {
        var domain = await _organizationDomainRepository.GetByIdAsync(id);
        if (domain is null)
        {
            throw new NotFoundException();
        }

        await _organizationDomainRepository.DeleteAsync(domain);
        await _eventService.LogOrganizationDomainEventAsync(domain, EventType.OrganizationDomain_Removed);
    }
}
