using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise;

public class CreateSponsorshipCommand : ICreateSponsorshipCommand
{
    private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
    private readonly IUserService _userService;

    public CreateSponsorshipCommand(IOrganizationSponsorshipRepository organizationSponsorshipRepository,
    IUserService userService)
    {
        _organizationSponsorshipRepository = organizationSponsorshipRepository;
        _userService = userService;
    }

    public async Task<OrganizationSponsorship> CreateSponsorshipAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
        PlanSponsorshipType sponsorshipType, string sponsoredEmail, string friendlyName)
    {
        var sponsoringUser = await _userService.GetUserByIdAsync(sponsoringOrgUser.UserId.Value);
        if (sponsoringUser == null || string.Equals(sponsoringUser.Email, sponsoredEmail, System.StringComparison.InvariantCultureIgnoreCase))
        {
            throw new BadRequestException("Cannot offer a Families Organization Sponsorship to yourself. Choose a different email.");
        }

        var requiredSponsoringProductType = StaticStore.GetSponsoredPlan(sponsorshipType)?.SponsoringProductType;
        if (requiredSponsoringProductType == null ||
            sponsoringOrg == null ||
            StaticStore.GetPlan(sponsoringOrg.PlanType).Product != requiredSponsoringProductType.Value)
        {
            throw new BadRequestException("Specified Organization cannot sponsor other organizations.");
        }

        if (sponsoringOrgUser == null || sponsoringOrgUser.Status != OrganizationUserStatusType.Confirmed)
        {
            throw new BadRequestException("Only confirmed users can sponsor other organizations.");
        }

        var existingOrgSponsorship = await _organizationSponsorshipRepository
            .GetBySponsoringOrganizationUserIdAsync(sponsoringOrgUser.Id);
        if (existingOrgSponsorship?.SponsoredOrganizationId != null)
        {
            throw new BadRequestException("Can only sponsor one organization per Organization User.");
        }

        var sponsorship = new OrganizationSponsorship
        {
            SponsoringOrganizationId = sponsoringOrg.Id,
            SponsoringOrganizationUserId = sponsoringOrgUser.Id,
            FriendlyName = friendlyName,
            OfferedToEmail = sponsoredEmail,
            PlanSponsorshipType = sponsorshipType,
        };

        if (existingOrgSponsorship != null)
        {
            // Replace existing invalid offer with our new sponsorship offer
            sponsorship.Id = existingOrgSponsorship.Id;
        }

        try
        {
            await _organizationSponsorshipRepository.UpsertAsync(sponsorship);
            return sponsorship;
        }
        catch
        {
            if (sponsorship.Id != default)
            {
                await _organizationSponsorshipRepository.DeleteAsync(sponsorship);
            }
            throw;
        }
    }
}
