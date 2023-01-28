using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities.Provider;
using Bit.Core.Settings;

namespace Bit.Api.Models.Request.Providers;

public class ProviderUpdateRequestModel
{
    [Required]
    [StringLength(50)]
    public string Name { get; set; }
    [StringLength(50)]
    public string BusinessName { get; set; }
    [EmailAddress]
    [Required]
    [StringLength(256)]
    public string BillingEmail { get; set; }

    public virtual Provider ToProvider(Provider existingProvider, GlobalSettings globalSettings)
    {
        if (!globalSettings.SelfHosted)
        {
            // These items come from the license file
            existingProvider.Name = Name;
            existingProvider.BusinessName = BusinessName;
            existingProvider.BillingEmail = BillingEmail?.ToLowerInvariant()?.Trim();
        }
        return existingProvider;
    }
}
