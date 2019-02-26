using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bit.EventsProcessor
{
    public class Functions
    {
        private readonly IEventWriteService _eventWriteService;

        public Functions(IConfiguration config)
        {
            var storageConnectionString = config["AzureWebJobsStorage"];
            if(string.IsNullOrWhiteSpace(storageConnectionString))
            {
                return;
            }

            var repo = new Core.Repositories.TableStorage.EventRepository(storageConnectionString);
            _eventWriteService = new RepositoryEventWriteService(repo);
        }

        public async Task ProcessQueueMessageAsync([QueueTrigger("event")] string message,
            CancellationToken cancellationToken, ILogger logger)
        {
            if(_eventWriteService == null || message == null || message.Length == 0)
            {
                return;
            }

            try
            {
                logger.LogInformation("Processing message.");
                var events = new List<IEvent>();

                var token = JToken.Parse(message);
                if(token is JArray)
                {
                    var indexedEntities = token.ToObject<List<EventMessage>>()
                        .SelectMany(e => EventTableEntity.IndexEvent(e));
                    events.AddRange(indexedEntities);
                }
                else if(token is JObject)
                {
                    var eventMessage = token.ToObject<EventMessage>();
                    events.AddRange(EventTableEntity.IndexEvent(eventMessage));
                }

                await _eventWriteService.CreateManyAsync(events);
                logger.LogInformation("Processed message.");
            }
            catch(JsonReaderException)
            {
                logger.LogError("JsonReaderException: Unable to parse message.");
            }
            catch(JsonSerializationException)
            {
                logger.LogError("JsonSerializationException: Unable to serialize token.");
            }
            catch(Exception e)
            {
                logger.LogError(e, "Exception occurred. " + e.Message);
                throw e;
            }
        }
    }
}
