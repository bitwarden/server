using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Client;
using Bit.Core.Domains;
using Bit.Core.Enums;

namespace Bit.Core.Repositories.DocumentDB
{
    public class SiteRepository : Repository<Site>, ISiteRepository
    {
        public SiteRepository(DocumentClient client, string databaseId)
            : base(client, databaseId)
        { }

        public async Task<Site> GetByIdAsync(string id, string userId)
        {
            var doc = await Client.ReadDocumentAsync(ResolveDocumentIdLink(userId, id));
            if(doc?.Resource == null)
            {
                return null;
            }

            var site = (Site)((dynamic)doc.Resource);
            if(site.UserId != userId)
            {
                return null;
            }

            return site;
        }

        public Task<ICollection<Site>> GetManyByUserIdAsync(string userId)
        {
            var docs = Client.CreateDocumentQuery<Site>(DatabaseUri, null, userId)
                .Where(d => d.Type == Cipher.TypeValue && d.CipherType == CipherType.Site && d.UserId == userId).AsEnumerable();

            return Task.FromResult<ICollection<Site>>(docs.ToList());
        }

        public Task<ICollection<Site>> GetManyByUserIdAsync(string userId, bool dirty)
        {
            var docs = Client.CreateDocumentQuery<Site>(DatabaseUri, null, userId)
                .Where(d => d.Type == Cipher.TypeValue && d.CipherType == CipherType.Site && d.UserId == userId && d.Dirty == dirty).AsEnumerable();

            return Task.FromResult<ICollection<Site>>(docs.ToList());
        }
    }
}
