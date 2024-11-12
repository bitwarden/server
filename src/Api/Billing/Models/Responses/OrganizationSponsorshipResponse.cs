using Bit.Core.AdminConsole.Entities;

namespace Bit.Api.Billing.Models.Responses;

public record OrganizationSponsorshipResponse(bool IsPolicyEnabled)
{
    public static OrganizationSponsorshipResponse From(Policy policy)
        => new(policy.Enabled);
}

