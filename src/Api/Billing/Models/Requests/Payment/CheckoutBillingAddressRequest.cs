using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Payment.Models;

namespace Bit.Api.Billing.Models.Requests.Payment;

public record CheckoutBillingAddressRequest : MinimalBillingAddressRequest
{
    public TaxIdRequest? TaxId { get; set; }

    public override BillingAddress ToDomain() => base.ToDomain() with
    {
        TaxId = TaxId != null ? new TaxID(TaxId.Code, TaxId.Value) : null
    };

    public class TaxIdRequest
    {
        [Required]
        public string Code { get; set; } = null!;

        [Required]
        public string Value { get; set; } = null!;
    }
}
