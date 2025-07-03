using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Models.Data;

#nullable enable

public class ServiceAccountSecretsDetails
{
    public required ServiceAccount ServiceAccount { get; set; }
    public int AccessToSecrets { get; set; }
}
