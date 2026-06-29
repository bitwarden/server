using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Enums;

namespace Bit.Admin.Auth.Models.SalesAssistedTrial;

public class SalesTrialInviteModel
{
    [Required]
    public string Email { get; set; } = null!;

    public string? Name { get; set; }

    [Required]
    public ProductTierType ProductTier { get; set; }

    [Required]
    [MinLength(1)]
    public IEnumerable<ProductType> Products { get; set; } = null!;

    [Required]
    [Range(0, 30)]
    public int TrialLength { get; set; }

    public bool PaymentOptional { get; set; }
}
