using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Client;
using Bit.Core.Domains;
using Bit.Core.Enums;
using Bit.Core.Repositories.DocumentDB.Utilities;
using Microsoft.Azure.Documents;

namespace Bit.Core.Repositories.DocumentDB
{
    public class SiteRepository : Repository<Site>, ISiteRepository
    {
        public SiteRepository(DocumentClient client, string databaseId)
            : base(client, databaseId)
        { }

        public async Task<Site> GetByIdAsync(string id, string userId)
        {
            ResourceResponse<Document> doc = null;
            var docLink = ResolveDocumentIdLink(userId, id);

            await DocumentDBHelpers.ExecuteWithRetryAsync(async () =>
            {
                doc = await Client.ReadDocumentAsync(docLink);
            });

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

        public async Task<ICollection<Site>> GetManyByUserIdAsync(string userId)
        {
            IEnumerable<Site> docs = null;
            await DocumentDBHelpers.ExecuteWithRetryAsync(() =>
            {
                docs = Client.CreateDocumentQuery<Site>(DatabaseUri, null, userId)
                    .Where(d => d.Type == Cipher.TypeValue && d.CipherType == CipherType.Site && d.UserId == userId).AsEnumerable();

                return Task.FromResult(0);
            });

            return docs.ToList();
        }

        public async Task<ICollection<Site>> GetManyByUserIdAsync(string userId, bool dirty)
        {
            IEnumerable<Site> docs = null;
            await DocumentDBHelpers.ExecuteWithRetryAsync(() =>
            {
                docs = Client.CreateDocumentQuery<Site>(DatabaseUri, null, userId)
                    .Where(d => d.Type == Cipher.TypeValue && d.CipherType == CipherType.Site && d.UserId == userId && d.Dirty == dirty).AsEnumerable();

                return Task.FromResult(0);
            });

            return docs.ToList();
        }
    }
}
