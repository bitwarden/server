#nullable enable

using Bit.Core.SecretsManager.Entities;

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
}
