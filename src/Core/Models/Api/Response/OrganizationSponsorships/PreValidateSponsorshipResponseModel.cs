namespace Bit.Core.Models.Api.Response.OrganizationSponsorships;

public record PreValidateSponsorshipResponseModel(
    bool IsTokenValid,
    bool IsFreeFamilyPolicyEnabled,
    string? SponsoringOrganizationName)
{
    public static PreValidateSponsorshipResponseModel From(bool validToken, bool policyStatus, string? sponsoringOrganizationName)
        => new(validToken, policyStatus, sponsoringOrganizationName);
}
