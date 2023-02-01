using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Enums.Provider;

public enum ProviderType : byte
{
    [Display(ShortName = "MSP", Name = "Managed Service Provider", Description = "Access to clients organization")]
    Msp = 0,
    [Display(ShortName = "Reseller", Name = "Reseller", Description = "Access to clients billing")]
    Reseller = 1,
}
