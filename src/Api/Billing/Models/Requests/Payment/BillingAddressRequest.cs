#nullable enable
using Bit.Core.Billing.Payment.Models;

namespace Bit.Api.Billing.Models.Requests.Payment;

public record BillingAddressRequest : CheckoutBillingAddressRequest
{
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }

    public override BillingAddress ToDomain() => base.ToDomain() with
    {
        Line1 = Line1,
        Line2 = Line2,
        City = City,
        State = State,
    };
}
