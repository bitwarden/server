#nullable enable

using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Repositories;

public interface ISecretVersionRepository
{
    Task<SecretVersion?> GetByIdAsync(Guid id);
    Task<IEnumerable<SecretVersion>> GetManyBySecretIdAsync(Guid secretId);
    Task<SecretVersion> CreateAsync(SecretVersion secretVersion);
    Task DeleteManyByIdAsync(IEnumerable<Guid> ids);
}
