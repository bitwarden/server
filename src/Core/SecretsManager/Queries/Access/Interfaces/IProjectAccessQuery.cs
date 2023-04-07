using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Queries.Access.Interfaces;

public interface IProjectAccessQuery
{
    Task<bool> HasAccess(AccessCheck accessCheck);
}
