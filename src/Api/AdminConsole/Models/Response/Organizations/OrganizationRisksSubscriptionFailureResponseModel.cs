using Bit.Core.Models.Api;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationRisksSubscriptionFailureResponseModel : ResponseModel
{
    public Guid OrganizationId { get; }
    public bool RisksSubscriptionFailure { get; }

    public OrganizationRisksSubscriptionFailureResponseModel(
        Guid organizationId,
        bool risksSubscriptionFailure) : base("organizationRisksSubscriptionFailure")
    {
        OrganizationId = organizationId;
        RisksSubscriptionFailure = risksSubscriptionFailure;
    }
}
