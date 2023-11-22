using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

public class ResetPasswordDataModel : IPolicyDataModel
{
    [Display(Name = "ResetPasswordAutoEnrollCheckbox")]
    public bool AutoEnrollEnabled { get; set; }
}
