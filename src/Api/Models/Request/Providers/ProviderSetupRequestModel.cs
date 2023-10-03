using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Bit.Core.Entities.Provider;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Request.Providers;

public class ProviderSetupRequestModel
{
    [Required]
    [StringLength(50)]
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string Name { get; set; }
    [StringLength(50)]
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

    public virtual Provider ToProvider(Provider provider)
    {
        provider.Name = Name;
        provider.BusinessName = BusinessName;
        provider.BillingEmail = BillingEmail;

        return provider;
    }
}
