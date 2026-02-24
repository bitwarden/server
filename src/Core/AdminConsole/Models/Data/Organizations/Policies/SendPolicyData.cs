using System.ComponentModel.DataAnnotations;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

public class SendPolicyData : IPolicyDataModel
{
    [Display(Name = "DisableSend")]
    public bool DisableSend { get; set; }

    [Display(Name = "DisableHideEmail")]
    public bool DisableHideEmail { get; set; }
}
