using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Client;

namespace Bit.Core.Repositories.DocumentDB
{
    public abstract class Repository<T> : BaseRepository<T>, IRepository<T> where T : IDataObject
    {
        public Repository(DocumentClient client, string databaseId, string documentType = null)
            : base(client, databaseId, documentType)
        { }

        public virtual Task<T> GetByIdAsync(string id)
        {
            // NOTE: Not an ideal condition, scanning all collections.
            // Override this method if you can implement a direct partition lookup based on the id.
            // Use the inherited GetByPartitionIdAsync method to implement your override.
            var docs = Client.CreateDocumentQuery<T>(DatabaseUri, new FeedOptions { MaxItemCount = 1 })
                .Where(d => d.Id == id).AsEnumerable();

            return Task.FromResult(docs.FirstOrDefault());
        }

        public virtual async Task CreateAsync(T obj)
        {
            var result = await Client.CreateDocumentAsync(DatabaseUri, obj);
            obj.Id = result.Resource.Id;
        }

        public virtual async Task ReplaceAsync(T obj)
        {
            await Client.ReplaceDocumentAsync(ResolveDocumentIdLink(obj), obj);
        }

        public virtual async Task UpsertAsync(T obj)
        {
            await Client.UpsertDocumentAsync(ResolveDocumentIdLink(obj), obj);
        }

        public virtual async Task DeleteAsync(T obj)
        {
            await Client.DeleteDocumentAsync(ResolveDocumentIdLink(obj));
        }

        public virtual async Task DeleteByIdAsync(string id)
        {
            // NOTE: Not an ideal condition, scanning all collections.
            // Override this method if you can implement a direct partition lookup based on the id.
            // Use the inherited DeleteByPartitionIdAsync method to implement your override.
            var docs = Client.CreateDocumentQuery(DatabaseUri, new FeedOptions { MaxItemCount = 1 })
                .Where(d => d.Id == id).AsEnumerable();

            if(docs.Count() > 0)
            {
                await Client.DeleteDocumentAsync(docs.First().SelfLink);
            }
        }

        protected async Task<T> GetByPartitionIdAsync(string id)
        {
            var doc = await Client.ReadDocumentAsync(ResolveDocumentIdLink(id));
            if(doc?.Resource == null)
            {
                return default(T);
            }

            return (T)((dynamic)doc.Resource);
        }

        protected async Task DeleteByPartitionIdAsync(string id)
        {
            await Client.DeleteDocumentAsync(ResolveDocumentIdLink(id));
        }
    }
}
