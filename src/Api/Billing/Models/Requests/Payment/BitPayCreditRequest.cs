using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Billing.Models.Requests.Payment;

public record BitPayCreditRequest
{
    [Required]
    public required decimal Amount { get; set; }

    [Required]
    public required string RedirectUrl { get; set; } = null!;
}
