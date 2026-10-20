// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json.Serialization;
using Bit.Core.Providers.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Providers.Models.Data;

public class ProviderOrganizationProviderDetails
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public Guid OrganizationId { get; set; }
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string ProviderName { get; set; }
    public ProviderType ProviderType { get; set; }
}
