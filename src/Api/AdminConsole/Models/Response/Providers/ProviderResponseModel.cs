using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Models.Api;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Response.Providers;

public class ProviderResponseModel : ResponseModel
{
    public ProviderResponseModel(Provider provider, string obj = "provider") : base(obj)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        Id = provider.Id;
        Name = provider.Name;
        BusinessName = provider.BusinessName;
        BusinessAddress1 = provider.BusinessAddress1;
        BusinessAddress2 = provider.BusinessAddress2;
        BusinessAddress3 = provider.BusinessAddress3;
        BusinessCountry = provider.BusinessCountry;
        BusinessTaxNumber = provider.BusinessTaxNumber;
        BillingEmail = provider.BillingEmail;
        CreationDate = provider.CreationDate;
        Type = provider.Type;
    }

    public Guid Id { get; set; }
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string Name { get; set; }
    public string BusinessName { get; set; }
    public string BusinessAddress1 { get; set; }
    public string BusinessAddress2 { get; set; }
    public string BusinessAddress3 { get; set; }
    public string BusinessCountry { get; set; }
    public string BusinessTaxNumber { get; set; }
    public string BillingEmail { get; set; }
    public DateTime CreationDate { get; set; }
    public ProviderType Type { get; set; }
}
