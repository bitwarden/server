using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Queries.Access.Interfaces;

public interface IAccessQuery
{
    Task<bool> HasAccess(AccessCheck accessCheck);
    Task<bool> HasAccess(SecretAccessCheck accessCheck);
}
