using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;

public class DeleteOrganizationDomainCommand : IDeleteOrganizationDomainCommand
{
    private readonly IOrganizationDomainRepository _organizationDomainRepository;
    private readonly IEventService _eventService;

    public DeleteOrganizationDomainCommand(
        IOrganizationDomainRepository organizationDomainRepository,
        IEventService eventService
    )
    {
        _organizationDomainRepository = organizationDomainRepository;
        _eventService = eventService;
    }

    public async Task DeleteAsync(OrganizationDomain organizationDomain)
    {
        await _organizationDomainRepository.DeleteAsync(organizationDomain);
        await _eventService.LogOrganizationDomainEventAsync(
            organizationDomain,
            EventType.OrganizationDomain_Removed
        );
    }
}
