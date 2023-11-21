using System.ComponentModel.DataAnnotations;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

public class ResetPasswordDataModel : IPolicyDataModel
{
    [Display(Name = "ResetPasswordAutoEnrollCheckbox")]
    public bool AutoEnrollEnabled { get; set; }
}
