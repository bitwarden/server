// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json.Serialization;
using Bit.Core.Providers.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Providers.Models.Data;

public class ProviderUserUserDetails
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public Guid? UserId { get; set; }
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string Name { get; set; }
    public string Email { get; set; }
    public ProviderUserStatusType Status { get; set; }
    public ProviderUserType Type { get; set; }
    public string Permissions { get; set; }
}
