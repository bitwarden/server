#nullable enable
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Queries.Secrets.Interfaces;

public interface ISecretsSyncQuery
{
    Task<(bool HasChanges, IEnumerable<Secret>? Secrets)> GetAsync(SecretsSyncRequest syncRequest);
}
