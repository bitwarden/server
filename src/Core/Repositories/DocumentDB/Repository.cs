using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Repositories.DocumentDB.Utilities;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace Bit.Core.Repositories.DocumentDB
{
    public abstract class Repository<T> : BaseRepository<T>, IRepository<T> where T : IDataObject
    {
        public Repository(DocumentClient client, string databaseId, string documentType = null)
            : base(client, databaseId, documentType)
        { }

        public virtual async Task<T> GetByIdAsync(string id)
        {
            // NOTE: Not an ideal condition, scanning all collections.
            // Override this method if you can implement a direct partition lookup based on the id.
            // Use the inherited GetByPartitionIdAsync method to implement your override.

            IEnumerable<T> docs = null;
            await DocumentDBHelpers.ExecuteWithRetryAsync(() =>
            {
                docs = Client.CreateDocumentQuery<T>(DatabaseUri, new FeedOptions { MaxItemCount = 1 })
                    .Where(d => d.Id == id).AsEnumerable();

                return Task.FromResult(0);
            });

            return docs.FirstOrDefault();
        }

        public virtual async Task CreateAsync(T obj)
        {
            await DocumentDBHelpers.ExecuteWithRetryAsync(async () =>
            {
                var result = await Client.CreateDocumentAsync(DatabaseUri, obj);
                obj.Id = result.Resource.Id;
            });
        }

        public virtual async Task ReplaceAsync(T obj)
        {
            var docLink = ResolveDocumentIdLink(obj);

            await DocumentDBHelpers.ExecuteWithRetryAsync(async () =>
            {
                await Client.ReplaceDocumentAsync(docLink, obj);
            });
        }

        public virtual async Task UpsertAsync(T obj)
        {
            var docLink = ResolveDocumentIdLink(obj);

            await DocumentDBHelpers.ExecuteWithRetryAsync(async () =>
            {
                await Client.UpsertDocumentAsync(docLink, obj);
            });
        }

        public virtual async Task DeleteAsync(T obj)
        {
            var docLink = ResolveDocumentIdLink(obj);

            await DocumentDBHelpers.ExecuteWithRetryAsync(async () =>
            {
                await Client.DeleteDocumentAsync(docLink);
            });
        }

        public virtual async Task DeleteByIdAsync(string id)
        {
            // NOTE: Not an ideal condition, scanning all collections.
            // Override this method if you can implement a direct partition lookup based on the id.
            // Use the inherited DeleteByPartitionIdAsync method to implement your override.

            IEnumerable<Document> docs = null;
            await DocumentDBHelpers.ExecuteWithRetryAsync(() =>
            {
                docs = Client.CreateDocumentQuery(DatabaseUri, new FeedOptions { MaxItemCount = 1 })
                    .Where(d => d.Id == id).AsEnumerable();

                return Task.FromResult(0);
            });

            if(docs != null && docs.Count() > 0)
            {
                await DocumentDBHelpers.ExecuteWithRetryAsync(async () =>
                {
                    await Client.DeleteDocumentAsync(docs.First().SelfLink);
                });
            }
        }

        protected async Task<T> GetByPartitionIdAsync(string id)
        {
            ResourceResponse<Document> doc = null;
            var docLink = ResolveDocumentIdLink(id);

            await DocumentDBHelpers.ExecuteWithRetryAsync(async () =>
            {
                doc = await Client.ReadDocumentAsync(docLink);
            });

            if(doc?.Resource == null)
            {
                return default(T);
            }

            return (T)((dynamic)doc.Resource);
        }

        protected async Task DeleteByPartitionIdAsync(string id)
        {
            var docLink = ResolveDocumentIdLink(id);

            await DocumentDBHelpers.ExecuteWithRetryAsync(async () =>
            {
                await Client.DeleteDocumentAsync(docLink);
            });
        }
    }
}
