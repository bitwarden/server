using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Repositories;

public interface ISecretVersionRepository
{
    Task<SecretVersion?> GetByIdAsync(Guid id);
    Task<IEnumerable<SecretVersion>> GetManyBySecretIdAsync(Guid secretId);
    Task<IEnumerable<SecretVersion>> GetManyByIdsAsync(IEnumerable<Guid> ids);
    Task<SecretVersion> CreateAsync(SecretVersion secretVersion);
    Task DeleteManyByIdAsync(IEnumerable<Guid> ids);
    Task<SecretVersionDetails?> GetDetailsByIdAsync(Guid id);
    Task<IEnumerable<SecretVersionDetails>> GetManyDetailsBySecretIdAsync(Guid secretId);
    Task<IEnumerable<SecretVersionDetails>> GetManyDetailsByIdsAsync(IEnumerable<Guid> ids);
}
