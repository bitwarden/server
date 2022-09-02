using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities.Provider;

namespace Bit.Api.Models.Request.Providers;

public class ProviderSetupRequestModel
{
    [Required]
    [StringLength(50)]
    public string Name { get; set; }
    [StringLength(50)]
    public string BusinessName { get; set; }
    [Required]
    [StringLength(256)]
    [EmailAddress]
    public string BillingEmail { get; set; }
    [Required]
    public string Token { get; set; }
    [Required]
    public string Key { get; set; }

    public virtual Provider ToProvider(Provider provider)
    {
        provider.Name = Name;
        provider.BusinessName = BusinessName;
        provider.BillingEmail = BillingEmail;

        return provider;
    }
}
