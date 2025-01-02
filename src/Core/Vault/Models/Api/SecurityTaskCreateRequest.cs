using System.Text.Json.Serialization;
using Bit.Core.Vault.Enums;

namespace Bit.Core.Vault.Models.Api;

public class SecurityTaskCreateRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SecurityTaskType Type { get; set; }
    public Guid CipherId { get; set; }
}
