using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Models.Data;

#nullable enable

public class SecretPermissionDetails
{
    public required Secret Secret { get; set; }
    public bool Read { get; set; }
    public bool Write { get; set; }
}
