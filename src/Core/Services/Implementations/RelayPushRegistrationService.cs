using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Settings;
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
            var requestModel = new PushRegistrationRequestModel
            {
                DeviceId = deviceId,
                Identifier = identifier,
                PushToken = pushToken,
                Type = type,
                UserId = userId
            };
            await SendAsync(HttpMethod.Post, "push/register", requestModel);
        }

        public async Task DeleteRegistrationAsync(string deviceId)
        {
            await SendAsync(HttpMethod.Delete, string.Concat("push/", deviceId));
        }

        public async Task AddUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId)
        {
            if (!deviceIds.Any())
            {
                return;
            }

            var requestModel = new PushUpdateRequestModel(deviceIds, organizationId);
            await SendAsync(HttpMethod.Put, "push/add-organization", requestModel);
        }

        public async Task DeleteUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId)
        {
            if (!deviceIds.Any())
            {
                return;
            }

            var requestModel = new PushUpdateRequestModel(deviceIds, organizationId);
            await SendAsync(HttpMethod.Put, "push/delete-organization", requestModel);
        }
    }
}
