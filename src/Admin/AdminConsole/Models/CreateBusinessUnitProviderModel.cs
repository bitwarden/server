// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Enums;
using Bit.SharedWeb.Utilities;

namespace Bit.Admin.AdminConsole.Models;

public class CreateBusinessUnitProviderModel : IValidatableObject
{
    [Display(Name = "Owner Email")]
    public string OwnerEmail { get; set; }

    [Display(Name = "Enterprise Seat Minimum")]
    public int EnterpriseSeatMinimum { get; set; }

    [Display(Name = "Plan")]
    [Required]
    public PlanType? Plan { get; set; }

    public virtual Provider ToProvider()
    {
        return new Provider
        {
            Type = ProviderType.BusinessUnit
        };
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(OwnerEmail))
        {
            var ownerEmailDisplayName = nameof(OwnerEmail).GetDisplayAttribute<CreateBusinessUnitProviderModel>()?.GetName() ?? nameof(OwnerEmail);
            yield return new ValidationResult($"The {ownerEmailDisplayName} field is required.");
        }
        if (EnterpriseSeatMinimum < 0)
        {
            var enterpriseSeatMinimumDisplayName = nameof(EnterpriseSeatMinimum).GetDisplayAttribute<CreateBusinessUnitProviderModel>()?.GetName() ?? nameof(EnterpriseSeatMinimum);
            yield return new ValidationResult($"The {enterpriseSeatMinimumDisplayName} field can not be negative.");
        }
        if (Plan != PlanType.EnterpriseAnnually && Plan != PlanType.EnterpriseMonthly)
        {
            var planDisplayName = nameof(Plan).GetDisplayAttribute<CreateBusinessUnitProviderModel>()?.GetName() ?? nameof(Plan);
            yield return new ValidationResult($"The {planDisplayName} field must be set to Enterprise Annually or Enterprise Monthly.");
        }
    }
}
