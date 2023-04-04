using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Models.Data;

public class SecretPermissionDetails
{
    public Secret Secret;
    public bool Read { get; set; }
    public bool Write { get; set; }
}
