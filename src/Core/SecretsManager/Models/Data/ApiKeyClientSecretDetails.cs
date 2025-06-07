using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Models.Data;

#nullable enable

public class ApiKeyClientSecretDetails
{
    public required ApiKey ApiKey { get; set; }
    public required string ClientSecret { get; set; }
}
