using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bit.EventsProcessor
{
    public class Functions
    {
        private static IEventWriteService _eventWriteService;

        static Functions()
        {
            var storageConnectionString = ConfigurationManager.ConnectionStrings["AzureWebJobsStorage"];
            if(storageConnectionString == null || string.IsNullOrWhiteSpace(storageConnectionString.ConnectionString))
            {
                return;
            }

            var repo = new Core.Repositories.TableStorage.EventRepository(storageConnectionString.ConnectionString);
            _eventWriteService = new RepositoryEventWriteService(repo);
        }

        public async static Task ProcessQueueMessageAsync([QueueTrigger("event")] string message,
            TextWriter logger, CancellationToken cancellationToken)
        {
            if(_eventWriteService == null || message == null || message.Length == 0)
            {
                return;
            }

            try
            {
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
            }
            catch(JsonReaderException)
            {
                await logger.WriteLineAsync("JsonReaderException: Unable to parse message.");
            }
            catch(JsonSerializationException)
            {
                await logger.WriteLineAsync("JsonSerializationException: Unable to serialize token.");
            }
        }
    }
}
