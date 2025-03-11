using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SponsorshipCreation;

public record CreateSponsorshipRequest(
    Organization SponsoringOrganization,
    OrganizationUser SponsoringMember,
    PlanSponsorshipType SponsorshipType,
    string SponsoredEmail,
    string FriendlyName);
