using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Models.Api.Requests.Accounts;

public class TrialSendVerificationEmailRequestModel : RegisterSendVerificationEmailRequestModel
{
    [Required]
    public ProductTierType ProductTier { get; init; }
    [Required]
    [MinLength(1)]
    public IEnumerable<ProductType> Products { get; init; } = null!;
    [Range(0, 30)]
    public int? TrialLength { get; init; }
    public bool PaymentOptional { get; init; }
}
