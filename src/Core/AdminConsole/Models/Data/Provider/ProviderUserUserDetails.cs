using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Models.Data.Provider;

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
