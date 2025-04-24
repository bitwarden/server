﻿using Bit.Core.AdminConsole.Entities;
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
    ICurrentContext currentContext,
    IOrganizationSponsorshipRepository organizationSponsorshipRepository,
    IUserService userService,
    IOrganizationService organizationService) : ICreateSponsorshipCommand
{
    public async Task<OrganizationSponsorship> CreateSponsorshipAsync(
        Organization sponsoringOrganization,
        OrganizationUser sponsoringMember,
        PlanSponsorshipType sponsorshipType,
        string sponsoredEmail,
        string friendlyName,
        bool isAdminInitiated,
        string notes)
    {
        var sponsoringUser = await userService.GetUserByIdAsync(sponsoringMember.UserId!.Value);

        if (sponsoringUser == null || string.Equals(sponsoringUser.Email, sponsoredEmail, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new BadRequestException("Cannot offer a Families Organization Sponsorship to yourself. Choose a different email.");
        }

        var requiredSponsoringProductType = StaticStore.GetSponsoredPlan(sponsorshipType)?.SponsoringProductTierType;
        var sponsoringOrgProductTier = sponsoringOrganization.PlanType.GetProductTier();

        if (requiredSponsoringProductType == null ||
            sponsoringOrgProductTier != requiredSponsoringProductType.Value)
        {
            throw new BadRequestException("Specified Organization cannot sponsor other organizations.");
        }

        if (sponsoringMember.Status != OrganizationUserStatusType.Confirmed)
        {
            throw new BadRequestException("Only confirmed users can sponsor other organizations.");
        }

        var existingOrgSponsorship = await organizationSponsorshipRepository
            .GetBySponsoringOrganizationUserIdAsync(sponsoringMember.Id);
        if (existingOrgSponsorship?.SponsoredOrganizationId != null)
        {
            throw new BadRequestException("Can only sponsor one organization per Organization User.");
        }

        if (isAdminInitiated)
        {
            ValidateAdminInitiatedSponsorship(sponsoringOrganization);
        }

        var sponsorship = new OrganizationSponsorship
        {
            SponsoringOrganizationId = sponsoringOrganization.Id,
            SponsoringOrganizationUserId = sponsoringMember.Id,
            FriendlyName = friendlyName,
            OfferedToEmail = sponsoredEmail,
            PlanSponsorshipType = sponsorshipType,
            IsAdminInitiated = isAdminInitiated,
            Notes = notes
        };

        if (existingOrgSponsorship != null)
        {
            // Replace existing invalid offer with our new sponsorship offer
            sponsorship.Id = existingOrgSponsorship.Id;
        }

        if (isAdminInitiated && sponsoringOrganization.Seats.HasValue)
        {
            await organizationService.AutoAddSeatsAsync(sponsoringOrganization, 1);
        }

        try
        {
            if (isAdminInitiated)
            {
                await organizationSponsorshipRepository.CreateAsync(sponsorship);
            }
            else
            {
                await organizationSponsorshipRepository.UpsertAsync(sponsorship);
            }

            return sponsorship;
        }
        catch
        {
            if (sponsorship.Id != Guid.Empty)
            {
                await organizationSponsorshipRepository.DeleteAsync(sponsorship);
            }
            throw;
        }
    }

    private void ValidateAdminInitiatedSponsorship(Organization sponsoringOrganization)
    {
        var organization = currentContext.Organizations.First(x => x.Id == sponsoringOrganization.Id);
        OrganizationUserType[] allowedUserTypes =
        [
            OrganizationUserType.Admin,
            OrganizationUserType.Owner
        ];

        if (!organization.Permissions.ManageUsers && allowedUserTypes.All(x => x != organization.Type))
        {
            throw new UnauthorizedAccessException("You do not have permissions to send sponsorships on behalf of the organization");
        }

        if (!sponsoringOrganization.UseAdminSponsoredFamilies)
        {
            throw new BadRequestException("Sponsoring organization cannot send admin-initiated sponsorship invitations");
        }
    }
}
