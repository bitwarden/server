using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Bit.Identity.IdentityServer;

public class FallbackPersistedGrantStore : IPersistedGrantStore
{
    private readonly IGrantRepository _grantRepository;
    private readonly Func<PersistedGrant, IGrant> _toGrant;
    private readonly IPersistedGrantStore _fallbackGrantStore;

    public FallbackPersistedGrantStore(
        IGrantRepository grantRepository,
        Func<PersistedGrant, IGrant> toGrant,
        IPersistedGrantStore fallbackGrantStore)
    {
        _grantRepository = grantRepository;
        _toGrant = toGrant;
        _fallbackGrantStore = fallbackGrantStore;
    }

    public async Task<PersistedGrant> GetAsync(string key)
    {
        var grant = await _grantRepository.GetByKeyAsync(key);
        if (grant == null)
        {
            if (_fallbackGrantStore != null)
            {
                // It wasn't found, there is a chance is was instead stored in the fallback store
                return await _fallbackGrantStore.GetAsync(key);
            }
            return null;
        }

        var pGrant = ToPersistedGrant(grant);
        return pGrant;
    }

    public Task<IEnumerable<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter)
    {
        return Task.FromResult(Enumerable.Empty<PersistedGrant>());
    }

    public Task RemoveAllAsync(PersistedGrantFilter filter)
    {
        return Task.CompletedTask;
    }

    public async Task RemoveAsync(string key)
    {
        await _grantRepository.DeleteByKeyAsync(key);
    }

    public async Task StoreAsync(PersistedGrant pGrant)
    {
        var grant = _toGrant(pGrant);
        await _grantRepository.SaveAsync(grant);
    }

    private PersistedGrant ToPersistedGrant(IGrant grant)
    {
        return new PersistedGrant
        {
            Key = grant.Key,
            Type = grant.Type,
            SubjectId = grant.SubjectId,
            SessionId = grant.SessionId,
            ClientId = grant.ClientId,
            Description = grant.Description,
            CreationTime = grant.CreationDate,
            Expiration = grant.ExpirationDate,
            ConsumedTime = grant.ConsumedDate,
            Data = grant.Data
        };
    }
}
