using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Models.Data;

public class ApiKeyClientSecretDetails
{
    public ApiKey ApiKey { get; set; }
    public string ClientSecret { get; set; }
}
