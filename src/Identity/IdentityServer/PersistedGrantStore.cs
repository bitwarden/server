using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Bit.Identity.IdentityServer;

public class PersistedGrantStore : IPersistedGrantStore
{
    private readonly IGrantRepository _grantRepository;
    private readonly Func<PersistedGrant, IGrant> _toGrant;

    public PersistedGrantStore(
        IGrantRepository grantRepository,
        Func<PersistedGrant, IGrant> toGrant)
    {
        _grantRepository = grantRepository;
        _toGrant = toGrant;
    }

    public async Task<PersistedGrant> GetAsync(string key)
    {
        var grant = await _grantRepository.GetByKeyAsync(key);
        if (grant == null)
        {
            return null;
        }

        var pGrant = ToPersistedGrant(grant);
        return pGrant;
    }

    public async Task<IEnumerable<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter)
    {
        var grants = await _grantRepository.GetManyAsync(filter.SubjectId, filter.SessionId,
            filter.ClientId, filter.Type);
        var pGrants = grants.Select(g => ToPersistedGrant(g));
        return pGrants;
    }

    public async Task RemoveAllAsync(PersistedGrantFilter filter)
    {
        await _grantRepository.DeleteManyAsync(filter.SubjectId, filter.SessionId, filter.ClientId, filter.Type);
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
