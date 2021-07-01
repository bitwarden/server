using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bit.Core;
using Bit.Core.Utilities;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Azure.Storage.Queues;
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
        private QueueClient _queueClient;
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
            _logger.LogInformation(Constants.BypassFiltersEventId, "Starting service.");
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executingTask = ExecuteAsync(_cts.Token);
            return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_executingTask == null)
            {
                return;
            }
            _logger.LogWarning("Stopping service.");
            _cts.Cancel();
            await Task.WhenAny(_executingTask, Task.Delay(-1, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
        }

        public void Dispose()
        { }

        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var storageConnectionString = _configuration["azureStorageConnectionString"];
            if (string.IsNullOrWhiteSpace(storageConnectionString))
            {
                return;
            }

            var repo = new Core.Repositories.TableStorage.EventRepository(storageConnectionString);
            _eventWriteService = new RepositoryEventWriteService(repo);
            _queueClient = new QueueClient(storageConnectionString, "event");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var messages = await _queueClient.ReceiveMessagesAsync(32);
                    if (messages.Value?.Any() ?? false)
                    {
                        foreach (var message in messages.Value)
                        {
                            await ProcessQueueMessageAsync(message.MessageText, cancellationToken);
                            await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                        }
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception occurred: " + e.Message);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }

            _logger.LogWarning("Done processing.");
        }

        public async Task ProcessQueueMessageAsync(string messageB64, CancellationToken cancellationToken)
        {
            if (_eventWriteService == null || messageB64 == null || messageB64.Length == 0)
            {
                return;
            }

            // Jul 1 2021: Catch needed for now until all messages are guaranteed to be B64 strings
            string message;
            try
            {
                message = CoreHelpers.Base64DecodeString(messageB64);
            }
            catch (FormatException)
            {
                message = messageB64;
            }

            try
            {
                _logger.LogInformation("Processing message.");
                var events = new List<IEvent>();

                var token = JToken.Parse(message);
                if (token is JArray)
                {
                    var indexedEntities = token.ToObject<List<EventMessage>>()
                        .SelectMany(e => EventTableEntity.IndexEvent(e));
                    events.AddRange(indexedEntities);
                }
                else if (token is JObject)
                {
                    var eventMessage = token.ToObject<EventMessage>();
                    events.AddRange(EventTableEntity.IndexEvent(eventMessage));
                }

                await _eventWriteService.CreateManyAsync(events);
                _logger.LogInformation("Processed message.");
            }
            catch (JsonReaderException)
            {
                _logger.LogError("JsonReaderException: Unable to parse message.");
            }
            catch (JsonSerializationException)
            {
                _logger.LogError("JsonSerializationException: Unable to serialize token.");
            }
        }
    }
}
