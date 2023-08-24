using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Models.Data;

public class ServiceAccountSecretsDetails
{
    public ServiceAccount ServiceAccount { get; set; }
    public int AccessToSecrets { get; set; }
}
