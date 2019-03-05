using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bit.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace Bit.Admin.HostedServices
{
    public class BlockIpHostedService : IHostedService, IDisposable
    {
        private readonly ILogger<BlockIpHostedService> _logger;
        private readonly GlobalSettings _globalSettings;
        public readonly AdminSettings _adminSettings;

        private Task _executingTask;
        private CancellationTokenSource _cts;
        private CloudQueue _blockQueue;
        private CloudQueue _unblockQueue;
        private HttpClient _httpClient = new HttpClient();

        public BlockIpHostedService(
            ILogger<BlockIpHostedService> logger,
            IOptions<AdminSettings> adminSettings,
            GlobalSettings globalSettings)
        {
            _logger = logger;
            _globalSettings = globalSettings;
            _adminSettings = adminSettings?.Value;
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
            var storageAccount = CloudStorageAccount.Parse(_globalSettings.Storage.ConnectionString);
            var queueClient = storageAccount.CreateCloudQueueClient();
            _blockQueue = queueClient.GetQueueReference("blockip");
            _unblockQueue = queueClient.GetQueueReference("unblockip");

            while(!cancellationToken.IsCancellationRequested)
            {
                var blockMessages = await _blockQueue.GetMessagesAsync(32, TimeSpan.FromSeconds(15),
                    null, null, cancellationToken);
                if(blockMessages.Any())
                {
                    foreach(var message in blockMessages)
                    {
                        try
                        {
                            await BlockIpAsync(message.AsString);
                        }
                        catch(Exception e)
                        {
                            _logger.LogError(e, "Failed to block IP.");
                        }
                        await _blockQueue.DeleteMessageAsync(message);
                    }
                }

                var unblockMessages = await _unblockQueue.GetMessagesAsync(32, TimeSpan.FromSeconds(15),
                    null, null, cancellationToken);
                if(unblockMessages.Any())
                {
                    foreach(var message in unblockMessages)
                    {
                        try
                        {
                            await UnblockIpAsync(message.AsString);
                        }
                        catch(Exception e)
                        {
                            _logger.LogError(e, "Failed to unblock IP.");
                        }
                        await _unblockQueue.DeleteMessageAsync(message);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        private async Task BlockIpAsync(string message)
        {
            var request = new HttpRequestMessage();
            request.Headers.Accept.Clear();
            request.Headers.Add("X-Auth-Email", _adminSettings.Cloudflare.AuthEmail);
            request.Headers.Add("X-Auth-Key", _adminSettings.Cloudflare.AuthKey);
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri("https://api.cloudflare.com/" +
                $"client/v4/zones/{_adminSettings.Cloudflare.ZoneId}/firewall/access_rules/rules");

            var bodyContent = JsonConvert.SerializeObject(new
            {
                mode = "block",
                configuration = new
                {
                    target = "ip",
                    value = message
                },
                notes = $"Rate limit abuse on {DateTime.UtcNow.ToString()}."
            });
            request.Content = new StringContent(bodyContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if(!response.IsSuccessStatusCode)
            {
                return;
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var accessRuleResponse = JsonConvert.DeserializeObject<AccessRuleResponse>(responseString);
            if(!accessRuleResponse.Success)
            {
                return;
            }

            // TODO: Send `accessRuleResponse.Result?.Id` message to unblock queue
        }

        private async Task UnblockIpAsync(string message)
        {
            if(string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if(message.Contains(".") || message.Contains(":"))
            {
                // IP address messages
                var request = new HttpRequestMessage();
                request.Headers.Accept.Clear();
                request.Headers.Add("X-Auth-Email", _adminSettings.Cloudflare.AuthEmail);
                request.Headers.Add("X-Auth-Key", _adminSettings.Cloudflare.AuthKey);
                request.Method = HttpMethod.Get;
                request.RequestUri = new Uri("https://api.cloudflare.com/" +
                    $"client/v4/zones/{_adminSettings.Cloudflare.ZoneId}/firewall/access_rules/rules?" +
                    $"configuration_target=ip&configuration_value={message}");

                var response = await _httpClient.SendAsync(request);
                if(!response.IsSuccessStatusCode)
                {
                    return;
                }

                var responseString = await response.Content.ReadAsStringAsync();
                var listResponse = JsonConvert.DeserializeObject<ListResponse>(responseString);
                if(!listResponse.Success)
                {
                    return;
                }

                foreach(var rule in listResponse.Result)
                {
                    if(rule.Configuration?.Value != message)
                    {
                        continue;
                    }

                    await DeleteAccessRuleAsync(rule.Id);
                }
            }
            else
            {
                // Rule Id messages
                await DeleteAccessRuleAsync(message);
            }
        }

        private async Task DeleteAccessRuleAsync(string ruleId)
        {
            var request = new HttpRequestMessage();
            request.Headers.Accept.Clear();
            request.Headers.Add("X-Auth-Email", _adminSettings.Cloudflare.AuthEmail);
            request.Headers.Add("X-Auth-Key", _adminSettings.Cloudflare.AuthKey);
            request.Method = HttpMethod.Delete;
            request.RequestUri = new Uri("https://api.cloudflare.com/" +
                $"client/v4/zones/{_adminSettings.Cloudflare.ZoneId}/firewall/access_rules/rules/{ruleId}");
            await _httpClient.SendAsync(request);
        }

        public class ListResponse
        {
            public bool Success { get; set; }
            public List<AccessRuleResultResponse> Result { get; set; }
        }

        public class AccessRuleResponse
        {
            public bool Success { get; set; }
            public AccessRuleResultResponse Result { get; set; }
        }

        public class AccessRuleResultResponse
        {
            public string Id { get; set; }
            public string Notes { get; set; }
            public ConfigurationResponse Configuration { get; set; }

            public class ConfigurationResponse
            {
                public string Target { get; set; }
                public string Value { get; set; }
            }
        }
    }
}
