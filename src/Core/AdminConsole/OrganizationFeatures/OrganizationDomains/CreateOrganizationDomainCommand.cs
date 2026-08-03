using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;

public class CreateOrganizationDomainCommand : ICreateOrganizationDomainCommand
{
    private readonly IOrganizationDomainRepository _organizationDomainRepository;
    private readonly IEventService _eventService;
    private readonly IGlobalSettings _globalSettings;

    public CreateOrganizationDomainCommand(
        IOrganizationDomainRepository organizationDomainRepository,
        IEventService eventService,
        IGlobalSettings globalSettings)
    {
        _organizationDomainRepository = organizationDomainRepository;
        _eventService = eventService;
        _globalSettings = globalSettings;
    }

    public async Task<OrganizationDomain> CreateAsync(OrganizationDomain organizationDomain)
    {
        //Domains claimed and verified by an organization cannot be claimed
        var claimedDomain =
            await _organizationDomainRepository.GetClaimedDomainsByDomainNameAsync(organizationDomain.DomainName);
        if (claimedDomain.Any())
        {
            throw new ConflictException("The domain is not available to be claimed.");
        }

        //check for duplicate domain entry for an organization
        var duplicateOrgDomain =
            await _organizationDomainRepository.GetDomainByOrgIdAndDomainNameAsync(organizationDomain.OrganizationId,
                organizationDomain.DomainName);
        if (duplicateOrgDomain is not null)
        {
            throw new ConflictException("A domain already exists for this organization.");
        }

        // Generate and set DNS TXT Record
        // DNS-Based Service Discovery RFC: https://www.ietf.org/rfc/rfc6763.txt; see section 6.1
        // Google uses 43 chars for their TXT record value: https://support.google.com/a/answer/2716802
        // A random 44 character string was used here to keep parity with prior client-side generation of 47 characters
        organizationDomain.Txt = string.Join("=", "bw", CoreHelpers.RandomString(44));
        organizationDomain.SetNextRunDate(_globalSettings.DomainVerification.VerificationInterval);

        var orgDomain = await _organizationDomainRepository.CreateAsync(organizationDomain);

        await _eventService.LogOrganizationDomainEventAsync(orgDomain, EventType.OrganizationDomain_Added);

        return orgDomain;
    }
}
