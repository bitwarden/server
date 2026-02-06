using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Payment.Models;

namespace Bit.Api.Billing.Models.Requests.Payment;

public record MinimalBillingAddressRequest
{
    [Required]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "Country code must be 2 characters long.")]
    public required string Country { get; set; } = null!;
    [Required]
    public required string PostalCode { get; set; } = null!;

    public virtual BillingAddress ToDomain() => new() { Country = Country, PostalCode = PostalCode, };
}
