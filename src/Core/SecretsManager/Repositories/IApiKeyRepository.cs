using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Repositories;

public interface IApiKeyRepository : IRepository<ApiKey, Guid>
{
    Task<ApiKeyDetails> GetDetailsByIdAsync(Guid id);
    Task<ICollection<ApiKey>> GetManyByServiceAccountIdAsync(Guid id);
    Task DeleteManyAsync(IEnumerable<ApiKey> objs);
}
