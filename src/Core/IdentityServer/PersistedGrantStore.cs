using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Repositories;
using IdentityServer4.Models;
using IdentityServer4.Stores;

namespace Bit.Core.IdentityServer
{
    public class PersistedGrantStore : IPersistedGrantStore
    {
        private readonly IGrantRepository _grantRepository;

        public PersistedGrantStore(
            IGrantRepository grantRepository)
        {
            _grantRepository = grantRepository;
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
            var grant = ToGrant(pGrant);
            await _grantRepository.SaveAsync(grant);
        }

        private Models.Table.Grant ToGrant(PersistedGrant pGrant)
        {
            return new Models.Table.Grant
            {
                Key = pGrant.Key,
                Type = pGrant.Type,
                SubjectId = pGrant.SubjectId,
                SessionId = pGrant.SessionId,
                ClientId = pGrant.ClientId,
                Description = pGrant.Description,
                CreationDate = pGrant.CreationTime,
                ExpirationDate = pGrant.Expiration,
                ConsumedDate = pGrant.ConsumedTime,
                Data = pGrant.Data
            };
        }

        private PersistedGrant ToPersistedGrant(Models.Table.Grant grant)
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
}
