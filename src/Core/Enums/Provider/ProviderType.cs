using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Enums.Provider;

public enum ProviderType : byte
{
    [Display(Name = "MSP")]
    Msp = 0,
    [Display(Name = "Reseller")]
    Reseller = 1,
}
