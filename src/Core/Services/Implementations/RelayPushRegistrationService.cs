using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System;
using Bit.Core.Models.Api;
using Bit.Core.Enums;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services
{
    public class RelayPushRegistrationService : BaseIdentityClientService, IPushRegistrationService
    {
        private readonly ILogger<RelayPushRegistrationService> _logger;

        public RelayPushRegistrationService(
            GlobalSettings globalSettings,
            ILogger<RelayPushRegistrationService> logger)
            : base(
                  globalSettings.PushRelayBaseUri,
                  globalSettings.Installation.IdentityUri,
                  "api.push",
                  $"installation.{globalSettings.Installation.Id}",
                  globalSettings.Installation.Key,
                  logger)
        {
            _logger = logger;
        }

        public async Task CreateOrUpdateRegistrationAsync(string pushToken, string deviceId, string userId,
            string identifier, DeviceType type)
        {
            var tokenStateResponse = await HandleTokenStateAsync();
            if(!tokenStateResponse)
            {
                return;
            }

            var requestModel = new PushRegistrationRequestModel
            {
                DeviceId = deviceId,
                Identifier = identifier,
                PushToken = pushToken,
                Type = type,
                UserId = userId
            };

            var message = new TokenHttpRequestMessage(requestModel, AccessToken)
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(string.Concat(Client.BaseAddress, "/push/register"))
            };

            try
            {
                await Client.SendAsync(message);
            }
            catch(Exception e)
            {
                _logger.LogError(12335, e, "Unable to create push registration.");
            }
        }

        public async Task DeleteRegistrationAsync(string deviceId)
        {
            var tokenStateResponse = await HandleTokenStateAsync();
            if(!tokenStateResponse)
            {
                return;
            }

            var message = new TokenHttpRequestMessage(AccessToken)
            {
                Method = HttpMethod.Delete,
                RequestUri = new Uri(string.Concat(Client.BaseAddress, "/push/", deviceId))
            };

            try
            {
                await Client.SendAsync(message);
            }
            catch(Exception e)
            {
                _logger.LogError(12336, e, "Unable to delete push registration.");
            }
        }

        public async Task AddUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId)
        {
            if(!deviceIds.Any())
            {
                return;
            }

            var tokenStateResponse = await HandleTokenStateAsync();
            if(!tokenStateResponse)
            {
                return;
            }

            var requestModel = new PushUpdateRequestModel(deviceIds, organizationId);
            var message = new TokenHttpRequestMessage(requestModel, AccessToken)
            {
                Method = HttpMethod.Put,
                RequestUri = new Uri(string.Concat(Client.BaseAddress, "/push/add-organization"))
            };

            try
            {
                await Client.SendAsync(message);
            }
            catch(Exception e)
            {
                _logger.LogError(12337, e, "Unable to add user org push registration.");
            }
        }

        public async Task DeleteUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId)
        {
            if(!deviceIds.Any())
            {
                return;
            }

            var tokenStateResponse = await HandleTokenStateAsync();
            if(!tokenStateResponse)
            {
                return;
            }

            var requestModel = new PushUpdateRequestModel(deviceIds, organizationId);
            var message = new TokenHttpRequestMessage(requestModel, AccessToken)
            {
                Method = HttpMethod.Put,
                RequestUri = new Uri(string.Concat(Client.BaseAddress, "/push/delete-organization"))
            };

            try
            {
                await Client.SendAsync(message);
            }
            catch(Exception e)
            {
                _logger.LogError(12338, e, "Unable to delete user org push registration.");
            }
        }
    }
}
