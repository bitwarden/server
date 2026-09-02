#nullable enable
using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Billing.Models.Requests.Premium;

public class PremiumSelfHostedSubscriptionRequest
{
    [Required]
    public required IFormFile License { get; set; }
}
