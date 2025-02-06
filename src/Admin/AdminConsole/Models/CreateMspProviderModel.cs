using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.SharedWeb.Utilities;

namespace Bit.Admin.AdminConsole.Models;

public class CreateMspProviderModel : IValidatableObject
{
    [Display(Name = "Owner Email")]
    public string OwnerEmail { get; set; }

    [Display(Name = "Teams (Monthly) Seat Minimum")]
    public int TeamsMonthlySeatMinimum { get; set; }

    [Display(Name = "Enterprise (Monthly) Seat Minimum")]
    public int EnterpriseMonthlySeatMinimum { get; set; }

    public virtual Provider ToProvider()
    {
        return new Provider
        {
            Type = ProviderType.Msp
        };
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(OwnerEmail))
        {
            var ownerEmailDisplayName = nameof(OwnerEmail).GetDisplayAttribute<CreateMspProviderModel>()?.GetName() ?? nameof(OwnerEmail);
            yield return new ValidationResult($"The {ownerEmailDisplayName} field is required.");
        }
        if (TeamsMonthlySeatMinimum < 0)
        {
            var teamsMinimumSeatsDisplayName = nameof(TeamsMonthlySeatMinimum).GetDisplayAttribute<CreateMspProviderModel>()?.GetName() ?? nameof(TeamsMonthlySeatMinimum);
            yield return new ValidationResult($"The {teamsMinimumSeatsDisplayName} field can not be negative.");
        }
        if (EnterpriseMonthlySeatMinimum < 0)
        {
            var enterpriseMinimumSeatsDisplayName = nameof(EnterpriseMonthlySeatMinimum).GetDisplayAttribute<CreateMspProviderModel>()?.GetName() ?? nameof(EnterpriseMonthlySeatMinimum);
            yield return new ValidationResult($"The {enterpriseMinimumSeatsDisplayName} field can not be negative.");
        }
    }
}
