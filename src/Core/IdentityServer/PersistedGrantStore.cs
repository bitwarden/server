using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
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

        public async Task<IEnumerable<PersistedGrant>> GetAllAsync(string subjectId)
        {
            var grants = await _grantRepository.GetManyAsync(subjectId);
            var pGrants = grants.Select(g => ToPersistedGrant(g));
            return pGrants;
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

        public async Task RemoveAllAsync(string subjectId, string clientId)
        {
            await _grantRepository.DeleteAsync(subjectId, clientId);
        }

        public async Task RemoveAllAsync(string subjectId, string clientId, string type)
        {
            await _grantRepository.DeleteAsync(subjectId, clientId, type);
        }

        public async Task RemoveAsync(string key)
        {
            await _grantRepository.DeleteAsync(key);
        }

        public async Task StoreAsync(PersistedGrant pGrant)
        {
            var grant = ToGrant(pGrant);
            await _grantRepository.SaveAsync(grant);
        }

        private Grant ToGrant(PersistedGrant pGrant)
        {
            return new Grant
            {
                Key = pGrant.Key,
                Type = pGrant.Type,
                SubjectId = pGrant.SubjectId,
                ClientId = pGrant.ClientId,
                CreationDate = pGrant.CreationTime,
                ExpirationDate = pGrant.Expiration,
                Data = pGrant.Data
            };
        }

        private PersistedGrant ToPersistedGrant(Grant grant)
        {
            return new PersistedGrant
            {
                Key = grant.Key,
                Type = grant.Type,
                SubjectId = grant.SubjectId,
                ClientId = grant.ClientId,
                CreationTime = grant.CreationDate,
                Expiration = grant.ExpirationDate,
                Data = grant.Data
            };
        }
    }
}
