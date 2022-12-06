using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.OrganizationFeatures.OrganizationDomains;

public class DeleteOrganizationDomainCommand : IDeleteOrganizationDomainCommand
{
    private readonly IOrganizationDomainRepository _organizationDomainRepository;

    public DeleteOrganizationDomainCommand(IOrganizationDomainRepository organizationDomainRepository)
    {
        _organizationDomainRepository = organizationDomainRepository;
    }

    public async Task DeleteAsync(Guid id)
    {
        var domain = await _organizationDomainRepository.GetByIdAsync(id);
        if (domain is null)
        {
            throw new NotFoundException();
        }

        await _organizationDomainRepository.DeleteAsync(domain);
    }
}
