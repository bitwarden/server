using Bit.Core.Vault.Enums;

namespace Bit.Core.Vault.Models.Api;

public class SecurityTaskCreateRequest
{
    public SecurityTaskType Type { get; set; }
    public Guid CipherId { get; set; }
}
