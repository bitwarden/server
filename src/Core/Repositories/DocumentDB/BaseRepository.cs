using System;
using Microsoft.Azure.Documents.Client;

namespace Bit.Core.Repositories.DocumentDB
{
    public abstract class BaseRepository<T> where T : IDataObject
    {
        public BaseRepository(DocumentClient client, string databaseId, string documentType = null)
        {
            Client = client;
            DatabaseId = databaseId;
            DatabaseUri = UriFactory.CreateDatabaseUri(databaseId);
            PartitionResolver = client.PartitionResolvers[DatabaseUri.OriginalString];

            if(string.IsNullOrWhiteSpace(documentType))
            {
                DocumentType = typeof(T).Name.ToLower();
            }
            else
            {
                DocumentType = documentType;
            }
        }

        protected DocumentClient Client { get; private set; }
        protected string DatabaseId { get; private set; }
        protected Uri DatabaseUri { get; private set; }
        protected IPartitionResolver PartitionResolver { get; private set; }
        protected string DocumentType { get; private set; }

        protected string ResolveSprocIdLink(T obj, string sprocId)
        {
            return string.Format("{0}/sprocs/{1}", ResolveCollectionIdLink(obj), sprocId);
        }

        protected string ResolveSprocIdLink(string partitionKey, string sprocId)
        {
            return string.Format("{0}/sprocs/{1}", ResolveCollectionIdLink(partitionKey), sprocId);
        }

        protected string ResolveDocumentIdLink(T obj)
        {
            return string.Format("{0}/docs/{1}", ResolveCollectionIdLink(obj), obj.Id);
        }

        protected string ResolveDocumentIdLink(string id)
        {
            return ResolveDocumentIdLink(id, id);
        }

        protected string ResolveDocumentIdLink(string partitionKey, string id)
        {
            return string.Format("{0}/docs/{1}", ResolveCollectionIdLink(partitionKey), id);
        }

        protected string ResolveCollectionIdLink(T obj)
        {
            var partitionKey = PartitionResolver.GetPartitionKey(obj);
            return ResolveCollectionIdLink(partitionKey);
        }

        protected string ResolveCollectionIdLink(object partitionKey)
        {
            return PartitionResolver.ResolveForCreate(partitionKey);
        }
    }
}
