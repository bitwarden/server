// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Models.Data;

public class ApiKeyClientSecretDetails
{
    public ApiKey ApiKey { get; set; }
    public string ClientSecret { get; set; }
}
