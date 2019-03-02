using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bit.EventsProcessor
{
    public class AzureQueueHostedService : IHostedService, IDisposable
    {
        private readonly ILogger<AzureQueueHostedService> _logger;
        private readonly IConfiguration _configuration;

        private Task _executingTask;
        private CancellationTokenSource _cts;
        private CloudQueue _queue;
        private IEventWriteService _eventWriteService;

        public AzureQueueHostedService(
            ILogger<AzureQueueHostedService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executingTask = ExecuteAsync(_cts.Token);
            return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if(_executingTask == null)
            {
                return;
            }
            _cts.Cancel();
            await Task.WhenAny(_executingTask, Task.Delay(-1, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
        }

        public void Dispose()
        { }

        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var storageConnectionString = _configuration["azureStorageConnectionString"];
            if(string.IsNullOrWhiteSpace(storageConnectionString))
            {
                return;
            }

            var repo = new Core.Repositories.TableStorage.EventRepository(storageConnectionString);
            _eventWriteService = new RepositoryEventWriteService(repo);

            var storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            var queueClient = storageAccount.CreateCloudQueueClient();
            _queue = queueClient.GetQueueReference("event");

            while(!cancellationToken.IsCancellationRequested)
            {
                var messages = await _queue.GetMessagesAsync(32, TimeSpan.FromMinutes(1),
                    null, null, cancellationToken);
                if(messages.Any())
                {
                    foreach(var message in messages)
                    {
                        await ProcessQueueMessageAsync(message.AsString, cancellationToken);
                        await _queue.DeleteMessageAsync(message);
                    }
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }

        public async Task ProcessQueueMessageAsync(string message, CancellationToken cancellationToken)
        {
            if(_eventWriteService == null || message == null || message.Length == 0)
            {
                return;
            }

            try
            {
                _logger.LogInformation("Processing message.");
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
                _logger.LogInformation("Processed message.");
            }
            catch(JsonReaderException)
            {
                _logger.LogError("JsonReaderException: Unable to parse message.");
            }
            catch(JsonSerializationException)
            {
                _logger.LogError("JsonSerializationException: Unable to serialize token.");
            }
            catch(Exception e)
            {
                _logger.LogError(e, "Exception occurred. " + e.Message);
                throw e;
            }
        }
    }
}
