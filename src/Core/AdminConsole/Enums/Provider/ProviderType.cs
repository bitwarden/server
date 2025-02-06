using System.ComponentModel.DataAnnotations;

namespace Bit.Core.AdminConsole.Enums.Provider;

public enum ProviderType : byte
{
    [Display(ShortName = "MSP", Name = "Managed Service Provider", Description = "Creates provider portal for client organization management", Order = 0)]
    Msp = 0,
    [Display(ShortName = "Reseller", Name = "Reseller", Description = "Creates Bitwarden Portal page for client organization billing management", Order = 1000)]
    Reseller = 1,
    [Display(ShortName = "MOE", Name = "Multi-organization Enterprises", Description = "Creates provider portal for multi-organization management", Order = 1)]
    MultiOrganizationEnterprise = 2,
}
