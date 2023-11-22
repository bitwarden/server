using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

public class SendOptionsPolicyData : IPolicyDataModel
{
    [Display(Name = "DisableHideEmail")]
    public bool DisableHideEmail { get; set; }
}
