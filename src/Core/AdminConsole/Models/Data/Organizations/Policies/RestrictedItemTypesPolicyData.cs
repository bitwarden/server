using System.ComponentModel.DataAnnotations;
using Bit.Core.Vault.Enums;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

public class RestrictedItemTypesPolicyData : IPolicyDataModel
{
    [Display(Name = "RestrictedItemTypes")]
    public ICollection<CipherType> RestrictedItemTypes { get; set; } = new List<CipherType>();
}
