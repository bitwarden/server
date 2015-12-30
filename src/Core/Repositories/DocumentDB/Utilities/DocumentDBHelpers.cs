using System;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace Bit.Core.Repositories.DocumentDB.Utilities
{
    public class DocumentDBHelpers
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

        public static async Task ExecuteWithRetryAsync(Func<Task> func, int? retryMax = null)
        {
            var executionAttempt = 1;
            while(true)
            {
                try
                {
                    await func();
                    return;
                }
                catch(DocumentClientException e)
                {
                    await HandleDocumentClientExceptionAsync(e, executionAttempt, retryMax);
                }
                catch(AggregateException e)
                {
                    var docEx = e.InnerException as DocumentClientException;
                    if(docEx != null)
                    {
                        await HandleDocumentClientExceptionAsync(docEx, executionAttempt, retryMax);
                    }
                }

                executionAttempt++;
            }
        }

        private static async Task HandleDocumentClientExceptionAsync(DocumentClientException e, int retryCount, int? retryMax)
        {
            if(retryMax.HasValue && retryCount >= retryMax.Value)
            {
                throw e;
            }

            var statusCode = (int)e.StatusCode;
            if(statusCode == 429 || statusCode == 503)
            {
                await Task.Delay(e.RetryAfter);
            }
            else {
                throw e;
            }
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
