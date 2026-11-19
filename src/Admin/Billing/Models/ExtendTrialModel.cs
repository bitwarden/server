using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Organizations.Models;

namespace Bit.Admin.Billing.Models;

public class ExtendTrialModel
{
    [Required(ErrorMessage = TrialExtensionPolicy.DaysRequiredMessage)]
    [Range(TrialExtensionPolicy.MinExtensionDays, TrialExtensionPolicy.MaxExtensionDays,
        ErrorMessage = TrialExtensionPolicy.DaysOutOfRangeMessage)]
    public int? Days { get; set; }
}
