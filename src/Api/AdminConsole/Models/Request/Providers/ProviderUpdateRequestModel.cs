using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Providers;

public class ProviderUpdateRequestModel
{
    [Required]
    [StringLength(50, ErrorMessage = "The field Name exceeds the maximum length.")]
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string Name { get; set; }

    [StringLength(50, ErrorMessage = "The field Business Name exceeds the maximum length.")]
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
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
