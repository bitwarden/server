using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationBillingStatusResponseModel(
    Organization organization,
    bool risksSubscriptionFailure) : ResponseModel("organizationBillingStatus")
{
    public Guid OrganizationId { get; } = organization.Id;
    public string OrganizationName { get; } = organization.Name;
    public bool RisksSubscriptionFailure { get; } = risksSubscriptionFailure;
}
