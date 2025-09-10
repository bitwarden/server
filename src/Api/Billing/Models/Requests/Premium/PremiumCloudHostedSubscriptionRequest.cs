#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Api.Billing.Models.Requests.Payment;
using Bit.Core.Billing.Payment.Models;

namespace Bit.Api.Billing.Models.Requests.Premium;

public class PremiumCloudHostedSubscriptionRequest
{
    [Required]
    public required MinimalTokenizedPaymentMethodRequest TokenizedPaymentMethod { get; set; }

    [Required]
    public required MinimalBillingAddressRequest BillingAddress { get; set; }

    [Range(0, 99)]
    public short AdditionalStorageGb { get; set; } = 0;

    public (TokenizedPaymentMethod, BillingAddress, short) ToDomain()
    {
        var paymentMethod = TokenizedPaymentMethod.ToDomain();
        var billingAddress = BillingAddress.ToDomain();

        return (paymentMethod, billingAddress, AdditionalStorageGb);
    }
}
