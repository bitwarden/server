using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Queries.Access.Interfaces;

public interface IProjectAccessQuery
{
    Task<bool> HasAccessToCreateAsync(AccessCheck accessCheck);
    Task<bool> HasAccessToUpdateAsync(AccessCheck accessCheck);
}
