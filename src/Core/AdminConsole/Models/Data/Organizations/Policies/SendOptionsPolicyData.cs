using System.ComponentModel.DataAnnotations;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

public class SendOptionsPolicyData : IPolicyDataModel
{
    [Display(Name = "DisableHideEmail")]
    public bool DisableHideEmail { get; set; }
}
