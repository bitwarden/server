using System.Text.Json.Serialization;
using Bit.Core.Enums.Provider;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Data;

public class ProviderOrganizationProviderDetails
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public Guid OrganizationId { get; set; }
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string ProviderName { get; set; }
    public ProviderType ProviderType { get; set; }
}
