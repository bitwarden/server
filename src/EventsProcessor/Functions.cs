using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
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
            TextWriter logger, CancellationToken token)
        {
            if(_eventWriteService == null || message == null || message.Length == 0)
            {
                return;
            }

            try
            {
                var jToken = JToken.Parse(message);
                if(jToken is JArray)
                {
                    var entities = jToken.ToObject<IList<EventTableEntity>>();
                    await _eventWriteService.CreateManyAsync(entities);
                }
                else if(jToken is JObject)
                {
                    var entity = jToken.ToObject<EventTableEntity>();
                    await _eventWriteService.CreateAsync(entity);
                }
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
