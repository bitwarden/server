using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Queries.Access.Interfaces;

public interface ISecretAccessQuery
{
    Task<bool> HasAccess(SecretAccessCheck accessCheck);
}
