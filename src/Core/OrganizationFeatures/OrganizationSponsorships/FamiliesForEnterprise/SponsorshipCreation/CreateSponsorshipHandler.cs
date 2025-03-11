using Bit.Core.Billing.Extensions;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SponsorshipCreation;

public class CreateSponsorshipHandler(
    IUserService userService,
    IOrganizationSponsorshipRepository organizationSponsorshipRepository) : BaseCreateSponsorshipHandler
{
    public override async Task<OrganizationSponsorship> HandleAsync(CreateSponsorshipRequest request)
    {
        var sponsoringUser = await userService.GetUserByIdAsync(request.SponsoringMember.UserId.Value);

        if (sponsoringUser == null || string.Equals(sponsoringUser.Email, request.SponsoredEmail, System.StringComparison.InvariantCultureIgnoreCase))
        {
            throw new BadRequestException("Cannot offer a Families Organization Sponsorship to yourself. Choose a different email.");
        }

        var requiredSponsoringProductType = StaticStore.GetSponsoredPlan(request.SponsorshipType)?.SponsoringProductTierType;
        var sponsoringOrgProductTier = request.SponsoringOrganization.PlanType.GetProductTier();

        if (requiredSponsoringProductType == null ||
            sponsoringOrgProductTier != requiredSponsoringProductType.Value)
        {
            throw new BadRequestException("Specified Organization cannot sponsor other organizations.");
        }

        if (request.SponsoringMember == null || request.SponsoringMember.Status != OrganizationUserStatusType.Confirmed)
        {
            throw new BadRequestException("Only confirmed users can sponsor other organizations.");
        }

        var existingOrgSponsorship = await organizationSponsorshipRepository
            .GetBySponsoringOrganizationUserIdAsync(request.SponsoringMember.Id);
        if (existingOrgSponsorship?.SponsoredOrganizationId != null)
        {
            throw new BadRequestException("Can only sponsor one organization per Organization User.");
        }

        var sponsorship = await base.HandleAsync(request) ?? new OrganizationSponsorship();

        sponsorship.SponsoringOrganizationId = request.SponsoringOrganization.Id;
        sponsorship.SponsoringOrganizationUserId = request.SponsoringMember.Id;
        sponsorship.FriendlyName = request.FriendlyName;
        sponsorship.OfferedToEmail = request.SponsoredEmail;
        sponsorship.PlanSponsorshipType = request.SponsorshipType;

        if (existingOrgSponsorship != null)
        {
            // Replace existing invalid offer with our new sponsorship offer
            sponsorship.Id = existingOrgSponsorship.Id;
        }

        return sponsorship;
    }
}
