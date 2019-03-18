using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bit.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Bit.Admin.HostedServices
{
    public abstract class BlockIpHostedService : IHostedService, IDisposable
    {
        protected readonly ILogger<BlockIpHostedService> _logger;
        protected readonly GlobalSettings _globalSettings;
        private readonly AdminSettings _adminSettings;

        private Task _executingTask;
        private CancellationTokenSource _cts;
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

        public virtual void Dispose()
        { }

        protected abstract Task ExecuteAsync(CancellationToken cancellationToken);

        protected async Task BlockIpAsync(string message, CancellationToken cancellationToken)
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

            var response = await _httpClient.SendAsync(request, cancellationToken);
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

        protected async Task UnblockIpAsync(string message, CancellationToken cancellationToken)
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

                var response = await _httpClient.SendAsync(request, cancellationToken);
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
                    await DeleteAccessRuleAsync(rule.Id, cancellationToken);
                }
            }
            else
            {
                // Rule Id messages
                await DeleteAccessRuleAsync(message, cancellationToken);
            }
        }

        protected async Task DeleteAccessRuleAsync(string ruleId, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage();
            request.Headers.Accept.Clear();
            request.Headers.Add("X-Auth-Email", _adminSettings.Cloudflare.AuthEmail);
            request.Headers.Add("X-Auth-Key", _adminSettings.Cloudflare.AuthKey);
            request.Method = HttpMethod.Delete;
            request.RequestUri = new Uri("https://api.cloudflare.com/" +
                $"client/v4/zones/{_adminSettings.Cloudflare.ZoneId}/firewall/access_rules/rules/{ruleId}");
            await _httpClient.SendAsync(request, cancellationToken);
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
