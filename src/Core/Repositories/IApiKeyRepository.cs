using Bit.Core.Entities;

namespace Bit.Core.Repositories;

public interface IApiKeyRepository : IRepository<ApiKey, Guid>
{
    Task<ICollection<ApiKey>> GetManyByServiceAccountIdAsync(Guid id);
}
