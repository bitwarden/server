using System.ComponentModel.DataAnnotations;

namespace Bit.Core.AdminConsole.Enums.Provider;

public enum ProviderType : byte
{
    [Display(ShortName = "MSP", Name = "Managed Service Provider", Description = "Access to clients organization", Order = 0)]
    Msp = 0,
    [Display(ShortName = "Reseller", Name = "Reseller", Description = "Access to clients billing", Order = 1000)]
    Reseller = 1,
    [Display(ShortName = "MOE", Name = "Multi-organization Enterprise", Description = "Access to multiple organizations", Order = 1)]
    MultiOrganizationEnterprise = 2,
}
