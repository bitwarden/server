using Bit.Api.Billing.Models.Requests.Organizations;
using Bit.Core.Billing.Organizations.Models;

namespace Bit.Api.Billing.Models.Requests.Tax;

public class PreviewOrganizationSubscriptionUpdateTaxRequest
{
    public required OrganizationSubscriptionUpdateRequest Update { get; set; }

    public OrganizationSubscriptionUpdate ToDomain() => Update.ToDomain();
}
