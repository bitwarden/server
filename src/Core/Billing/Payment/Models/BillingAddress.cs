using Stripe;

namespace Bit.Core.Billing.Payment.Models;

public record TaxID(string Code, string Value);

public record BillingAddress
{
    public required string Country { get; set; }
    public required string PostalCode { get; set; }
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public TaxID? TaxId { get; set; }

    public static BillingAddress From(Address address) => new()
    {
        Country = address.Country,
        PostalCode = address.PostalCode,
        Line1 = address.Line1,
        Line2 = address.Line2,
        City = address.City,
        State = address.State
    };

    public static BillingAddress From(Address address, TaxId? taxId) =>
        From(address) with { TaxId = taxId != null ? new TaxID(taxId.Type, taxId.Value) : null };
}
