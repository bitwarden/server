using System.ComponentModel.DataAnnotations;

namespace Bit.Core.AdminConsole.Enums.Provider;

public enum ProviderType : byte
{
    [Display(ShortName = "MSP", Name = "Managed Service Provider", Description = "Access to clients organization")]
    Msp = 0,
    [Display(ShortName = "Reseller", Name = "Reseller", Description = "Access to clients billing")]
    Reseller = 1,
    [Display(ShortName = "MOE", Name = "Multi-organization Enterprise", Description = "Access to multiple organizations")]
    MultiOrganizationEnterprise = 2,
}
