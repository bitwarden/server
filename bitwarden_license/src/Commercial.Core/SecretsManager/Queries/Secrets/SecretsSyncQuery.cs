#nullable enable
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Queries.Secrets.Interfaces;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Queries.Secrets;

public class SecretsSyncQuery : ISecretsSyncQuery
{
    private readonly ISecretRepository _secretRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public SecretsSyncQuery(
        ISecretRepository secretRepository,
        IServiceAccountRepository serviceAccountRepository)
    {
        _secretRepository = secretRepository;
        _serviceAccountRepository = serviceAccountRepository;
    }

    public async Task<(bool HasChanges, IEnumerable<Secret>? Secrets)> GetAsync(SecretsSyncRequest syncRequest)
    {
        if (syncRequest.LastSyncedDate == null)
        {
            return await GetSecretsAsync(syncRequest);
        }

        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(syncRequest.ServiceAccountId);
        if (serviceAccount == null)
        {
            throw new NotFoundException();
        }

        if (syncRequest.LastSyncedDate.Value <= serviceAccount.RevisionDate)
        {
            return await GetSecretsAsync(syncRequest);
        }

        return (HasChanges: false, null);
    }

    private async Task<(bool HasChanges, IEnumerable<Secret>? Secrets)> GetSecretsAsync(SecretsSyncRequest syncRequest)
    {
        var secrets = await _secretRepository.GetManyByOrganizationIdAsync(syncRequest.OrganizationId,
            syncRequest.ServiceAccountId, syncRequest.AccessClientType);
        return (HasChanges: true, Secrets: secrets);
    }
}
