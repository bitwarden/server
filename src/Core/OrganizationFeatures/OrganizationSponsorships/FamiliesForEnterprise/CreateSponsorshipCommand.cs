using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Extensions;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise;

public class CreateSponsorshipCommand(
    IOrganizationSponsorshipRepository organizationSponsorshipRepository,
    IUserService userService,
    ICurrentContext currentContext)
    : ICreateSponsorshipCommand
{
    public async Task<OrganizationSponsorship> CreateSponsorshipAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
        PlanSponsorshipType sponsorshipType, string sponsoredEmail, string friendlyName)
    {
        var sponsoringUser = await userService.GetUserByIdAsync(sponsoringOrgUser.UserId.Value);
        if (sponsoringUser == null || string.Equals(sponsoringUser.Email, sponsoredEmail, System.StringComparison.InvariantCultureIgnoreCase))
        {
            throw new BadRequestException("Cannot offer a Families Organization Sponsorship to yourself. Choose a different email.");
        }

        var requiredSponsoringProductType = StaticStore.GetSponsoredPlan(sponsorshipType)?.SponsoringProductTierType;
        var sponsoringOrgProductTier = sponsoringOrg.PlanType.GetProductTier();

        if (requiredSponsoringProductType == null ||
            sponsoringOrgProductTier != requiredSponsoringProductType.Value)
        {
            throw new BadRequestException("Specified Organization cannot sponsor other organizations.");
        }

        if (sponsoringOrgUser == null || sponsoringOrgUser.Status != OrganizationUserStatusType.Confirmed)
        {
            throw new BadRequestException("Only confirmed users can sponsor other organizations.");
        }

        var isAdminInitiated = false;
        if (currentContext.UserId != sponsoringOrgUser.UserId)
        {
            var organization = currentContext.Organizations.First(x => x.Id == sponsoringOrg.Id);
            OrganizationUserType[] allowedUserTypes =
            [
                OrganizationUserType.Admin,
                OrganizationUserType.Owner,
                OrganizationUserType.Custom
            ];
            if (!organization.Permissions.ManageUsers || allowedUserTypes.All(x => x != organization.Type))
            {
                throw new UnauthorizedAccessException("You do not have permissions to send sponsorships on behalf of the organization.");
            }
            isAdminInitiated = true;
        }

        var existingOrgSponsorship = await organizationSponsorshipRepository
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
            IsAdminInitiated = isAdminInitiated
        };

        if (existingOrgSponsorship != null)
        {
            // Replace existing invalid offer with our new sponsorship offer
            sponsorship.Id = existingOrgSponsorship.Id;
        }

        try
        {
            await organizationSponsorshipRepository.UpsertAsync(sponsorship);
            return sponsorship;
        }
        catch
        {
            if (sponsorship.Id != default)
            {
                await organizationSponsorshipRepository.DeleteAsync(sponsorship);
            }
            throw;
        }
    }
}
