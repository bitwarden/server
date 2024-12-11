using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Bit.Api.Models.Request;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Providers;

public class ProviderSetupRequestModel
{
    [Required]
    [StringLength(50, ErrorMessage = "The field Name exceeds the maximum length.")]
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string Name { get; set; }

    [StringLength(50, ErrorMessage = "The field Business Name exceeds the maximum length.")]
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string BusinessName { get; set; }

    [Required]
    [StringLength(256)]
    [EmailAddress]
    public string BillingEmail { get; set; }

    [Required]
    public string Token { get; set; }

    [Required]
    public string Key { get; set; }
    public ExpandedTaxInfoUpdateRequestModel TaxInfo { get; set; }

    public virtual Provider ToProvider(Provider provider)
    {
        provider.Name = Name;
        provider.BusinessName = BusinessName;
        provider.BillingEmail = BillingEmail;

        return provider;
    }
}
