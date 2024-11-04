using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

public class SendOptionsPolicyData : IPolicyDataModel
{
    [Display(Name = "DisableHideEmail")]
    public bool DisableHideEmail { get; set; }
}
