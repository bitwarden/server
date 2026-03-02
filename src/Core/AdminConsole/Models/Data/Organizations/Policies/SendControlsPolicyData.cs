using System.ComponentModel.DataAnnotations;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

public class SendControlsPolicyData : IPolicyDataModel
{
    [Display(Name = "DisableSend")]
    public bool DisableSend { get; set; }

    [Display(Name = "DisableHideEmail")]
    public bool DisableHideEmail { get; set; }
}
