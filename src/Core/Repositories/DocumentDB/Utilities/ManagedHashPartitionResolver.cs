using System;
using System.Collections.Generic;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Partitioning;

namespace Bit.Core.Repositories.DocumentDB.Utilities
{
    public class ManagedHashPartitionResolver : HashPartitionResolver
    {
        public ManagedHashPartitionResolver(
            Func<object, string> partitionKeyExtractor,
            string databaseId,
            string collectionIdPrefix,
            int numberOfCollections,
            IHashGenerator hashGenerator = null)
            : base(
                partitionKeyExtractor,
                GetCollectionIds(databaseId, collectionIdPrefix, numberOfCollections),
                128,
                hashGenerator)
        { }

        private static List<string> GetCollectionIds(string databaseId, string collectionIdPrefix, int numberOfCollections)
        {
            var collections = new List<string>();
            for(int i = 0; i < numberOfCollections; i++)
            {
                var collectionIdUri = UriFactory.CreateDocumentCollectionUri(databaseId, string.Concat(collectionIdPrefix, i));
                collections.Add(collectionIdUri.OriginalString);
            }

            return collections;
        }
    }
}
