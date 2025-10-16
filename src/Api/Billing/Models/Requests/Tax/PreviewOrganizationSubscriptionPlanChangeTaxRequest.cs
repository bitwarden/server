using System.ComponentModel.DataAnnotations;
using Bit.Api.Billing.Models.Requests.Organizations;
using Bit.Api.Billing.Models.Requests.Payment;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Payment.Models;

namespace Bit.Api.Billing.Models.Requests.Tax;

public record PreviewOrganizationSubscriptionPlanChangeTaxRequest
{
    [Required]
    public required OrganizationSubscriptionPlanChangeRequest Plan { get; set; }

    [Required]
    public required CheckoutBillingAddressRequest BillingAddress { get; set; }

    public (OrganizationSubscriptionPlanChange, BillingAddress) ToDomain() =>
        (Plan.ToDomain(), BillingAddress.ToDomain());
}
