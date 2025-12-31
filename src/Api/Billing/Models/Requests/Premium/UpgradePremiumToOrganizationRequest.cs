using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Enums;

namespace Bit.Api.Billing.Models.Requests.Premium;

public class UpgradePremiumToOrganizationRequest
{
    [Required]
    public required PlanType PlanType { get; set; }

    [Range(1, int.MaxValue)]
    public int Seats { get; set; }

    public bool PremiumAccess { get; set; } = false;

    [Range(0, 99)]
    public int Storage { get; set; } = 0;

    public DateTime? TrialEndDate { get; set; }

    public (PlanType, int, bool, int?, DateTime?) ToDomain() =>
        (PlanType, Seats, PremiumAccess, Storage > 0 ? Storage : null, TrialEndDate);
}
