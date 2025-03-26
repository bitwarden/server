using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SponsorshipCreation;

/// <summary>
/// Responsible for validating a request and building the <see cref="OrganizationSponsorship" /> entity to create a
/// sponsorship initiated by organization members with specific permissions to manage members/users.
/// </summary>
public class CreateAdminInitiatedSponsorshipHandler(
    ICurrentContext currentContext,
    IFeatureService featureService) : BaseCreateSponsorshipHandler
{
    public override async Task<OrganizationSponsorship> HandleAsync(CreateSponsorshipRequest request)
    {
        var isAdminInitiated = false;
        if (currentContext.UserId != request.SponsoringMember.UserId)
        {
            if (!featureService.IsEnabled(FeatureFlagKeys.PM17772_AdminInitiatedSponsorships))
            {
                throw new BadRequestException("Feature 'pm-17772-admin-initiated-sponsorships' is not enabled.");
            }

            var organization = currentContext.Organizations.First(x => x.Id == request.SponsoringOrganization.Id);
            OrganizationUserType[] allowedUserTypes =
            [
                OrganizationUserType.Admin,
                OrganizationUserType.Owner
            ];

            if (!organization.Permissions.ManageUsers && allowedUserTypes.All(x => x != organization.Type))
            {
                throw new UnauthorizedAccessException("You do not have permissions to send sponsorships on behalf of the organization.");
            }

            if (!request.SponsoringOrganization.UseAdminSponsoredFamilies)
            {
                throw new BadRequestException("Sponsoring organization cannot sponsor other Family organizations.");
            }

            isAdminInitiated = true;
        }

        var sponsorship = await base.HandleAsync(request) ?? new OrganizationSponsorship();

        sponsorship.IsAdminInitiated = isAdminInitiated;
        sponsorship.Notes = request.Notes;

        return sponsorship;
    }
}
