using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories;

public interface IApiKeyRepository : IRepository<ApiKey, Guid>
{
    Task<ApiKeyDetails> GetDetailsByIdAsync(Guid id);
    Task<ICollection<ApiKey>> GetManyByServiceAccountIdAsync(Guid id);
}
