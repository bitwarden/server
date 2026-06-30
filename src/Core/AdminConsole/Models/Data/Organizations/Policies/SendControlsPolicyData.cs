using System.ComponentModel.DataAnnotations;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

public class SendControlsPolicyData : IPolicyDataModel
{
    [Display(Name = "DisableSend")]
    public bool DisableSend { get; set; }
    [Display(Name = "DisableHideEmail")]
    public bool DisableHideEmail { get; set; }
    [Display(Name = "AllowedAccessControl")]
    public SendWhoCanAccessType? WhoCanAccess { get; set; }
    [Display(Name = "AllowedDomains")]
    [StringLength(1000)]
    public string? AllowedDomains { get; set; }
}
