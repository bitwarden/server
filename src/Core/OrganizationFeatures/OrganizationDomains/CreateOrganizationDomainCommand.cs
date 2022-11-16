using Bit.Core.Entities;
using Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.OrganizationFeatures.OrganizationDomains;

public class CreateOrganizationDomainCommand : ICreateOrganizationDomainCommand
{
    private readonly IOrganizationDomainRepository _organizationDomainRepository;

    public CreateOrganizationDomainCommand(IOrganizationDomainRepository organizationDomainRepository)
    {
        _organizationDomainRepository = organizationDomainRepository;
    }

    public async Task<OrganizationDomain> CreateAsync(OrganizationDomain organizationDomain)
    {
        //check the domain has not been claimed
        var claimedDomain =
            await _organizationDomainRepository.GetClaimedDomainsByDomainNameAsync(organizationDomain.DomainName);
        if (claimedDomain is not null)
        {
            // throw exception
        }
        //set initial nextRunDate and nextRunCount
        organizationDomain.SetNextRunCount(organizationDomain.NextRunCount)
            .SetNextRunDate();

        //create add domain
        return await _organizationDomainRepository.CreateAsync(organizationDomain);
    }
}
