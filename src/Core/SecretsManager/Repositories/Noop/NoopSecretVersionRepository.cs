using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Repositories.Noop;

public class NoopSecretVersionRepository : ISecretVersionRepository
{
    public Task<SecretVersion?> GetByIdAsync(Guid id)
    {
        return Task.FromResult(null as SecretVersion);
    }

    public Task<IEnumerable<SecretVersion>> GetManyBySecretIdAsync(Guid secretId)
    {
        return Task.FromResult(Enumerable.Empty<SecretVersion>());
    }

    public Task<SecretVersion> CreateAsync(SecretVersion secretVersion)
    {
        return Task.FromResult(secretVersion);
    }

    public Task DeleteManyByIdAsync(IEnumerable<Guid> ids)
    {
        return Task.CompletedTask;
    }

    public Task<IEnumerable<SecretVersion>> GetManyByIdsAsync(IEnumerable<Guid> ids)
    {
        return Task.FromResult(Enumerable.Empty<SecretVersion>());
    }

    public Task<SecretVersionDetails?> GetDetailsByIdAsync(Guid id)
    {
        return Task.FromResult(null as SecretVersionDetails);
    }

    public Task<IEnumerable<SecretVersionDetails>> GetManyDetailsBySecretIdAsync(Guid secretId)
    {
        return Task.FromResult(Enumerable.Empty<SecretVersionDetails>());
    }

    public Task<IEnumerable<SecretVersionDetails>> GetManyDetailsByIdsAsync(IEnumerable<Guid> ids)
    {
        return Task.FromResult(Enumerable.Empty<SecretVersionDetails>());
    }
}
