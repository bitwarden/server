// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Models.Data;

public class SecretPermissionDetails
{
    public Secret Secret { get; set; }
    public bool Read { get; set; }
    public bool Write { get; set; }
}
