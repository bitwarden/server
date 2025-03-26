using Bit.Core.AdminConsole.Entities;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SponsorshipCreation;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise;

public class CreateSponsorshipCommand : ICreateSponsorshipCommand
{
    private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;

    private readonly BaseCreateSponsorshipHandler _createSponsorshipHandler;

    public CreateSponsorshipCommand(
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IUserService userService,
        ICurrentContext currentContext)
    {
        _organizationSponsorshipRepository = organizationSponsorshipRepository;

        var adminInitiatedSponsorshipHandler = new CreateAdminInitiatedSponsorshipHandler(currentContext);
        _createSponsorshipHandler = new CreateSponsorshipHandler(userService, organizationSponsorshipRepository);
        _createSponsorshipHandler.SetNext(adminInitiatedSponsorshipHandler);
    }

    public async Task<OrganizationSponsorship> CreateSponsorshipAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
        PlanSponsorshipType sponsorshipType, string sponsoredEmail, string friendlyName, string notes)
    {
        var createSponsorshipRequest = new CreateSponsorshipRequest(sponsoringOrg, sponsoringOrgUser, sponsorshipType, sponsoredEmail, friendlyName, notes);
        var sponsorship = await _createSponsorshipHandler.HandleAsync(createSponsorshipRequest);

        try
        {
            await _organizationSponsorshipRepository.UpsertAsync(sponsorship);
            return sponsorship;
        }
        catch
        {
            if (sponsorship.Id != Guid.Empty)
            {
                await _organizationSponsorshipRepository.DeleteAsync(sponsorship);
            }
            throw;
        }
    }
}
