using System;
using Microsoft.Azure.Documents.Client;

namespace Bit.Core.Repositories.DocumentDB.Utilities
{
    public class DocumentClientHelpers
    {
        public static DocumentClient InitClient(GlobalSettings.DocumentDBSettings settings)
        {
            var client = new DocumentClient(
                new Uri(settings.Uri),
                settings.Key,
                new ConnectionPolicy
                {
                    ConnectionMode = ConnectionMode.Direct,
                    ConnectionProtocol = Protocol.Tcp
                });

            var hashResolver = new ManagedHashPartitionResolver(
                GetPartitionKeyExtractor(),
                settings.DatabaseId,
                settings.CollectionIdPrefix,
                settings.NumberOfCollections,
                null);

            client.PartitionResolvers[UriFactory.CreateDatabaseUri(settings.DatabaseId).OriginalString] = hashResolver;
            client.OpenAsync().Wait();

            return client;
        }

        private static Func<object, string> GetPartitionKeyExtractor()
        {
            return doc =>
            {
                if(doc is Domains.User)
                {
                    return ((Domains.User)doc).Id;
                }

                if(doc is Domains.Cipher)
                {
                    return ((Domains.Cipher)doc).UserId;
                }

                throw new InvalidOperationException("Document type is not resolvable for the partition key extractor.");
            };
        }
    }
}
