using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System;
using Bit.Core.Models.Api;
using Bit.Core.Enums;
using System.Linq;

namespace Bit.Core.Services
{
    public class RelayPushRegistrationService : BaseRelayPushNotificationService, IPushRegistrationService
    {
        private dynamic _decodedToken;
        private DateTime? _nextAuthAttempt = null;

        public RelayPushRegistrationService(GlobalSettings globalSettings)
            : base(globalSettings)
        { }

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
                RequestUri = new Uri(PushClient.BaseAddress, "register")
            };
            await PushClient.SendAsync(message);
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
                RequestUri = new Uri(PushClient.BaseAddress, deviceId)
            };
            await PushClient.SendAsync(message);
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
                RequestUri = new Uri(PushClient.BaseAddress, "add-organization")
            };
            await PushClient.SendAsync(message);
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
                RequestUri = new Uri(PushClient.BaseAddress, "delete-organization")
            };
            await PushClient.SendAsync(message);
        }
    }
}
