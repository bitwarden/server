namespace Bit.Core.Models.Api.Response.OrganizationSponsorships;

public record PreValidateSponsorshipResponseModel(bool IsTokenValid, bool IsFreeFamilyPolicyEnabled)
{
    public static PreValidateSponsorshipResponseModel From(bool validToken, bool policyStatus) =>
        new(validToken, policyStatus);
}
