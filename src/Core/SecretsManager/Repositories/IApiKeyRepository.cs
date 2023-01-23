using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Repositories;

public interface IApiKeyRepository : IRepository<ApiKey, Guid>
{
    Task<ApiKeyDetails> GetDetailsByIdAsync(Guid id);
    Task<ICollection<ApiKey>> GetManyByServiceAccountIdAsync(Guid id);
}
