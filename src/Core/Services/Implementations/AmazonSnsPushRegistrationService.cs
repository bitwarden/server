using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Enums;
using System.Linq;
using System;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Amazon.SimpleNotificationService.Model;
using Amazon.SimpleNotificationService;
using Amazon;
using System.Text.RegularExpressions;

namespace Bit.Core.Services
{
    public class AmazonSnsPushRegistrationService : IPushRegistrationService
    {
        private readonly IMetaDataRepository _metaDataRepository;
        private readonly GlobalSettings _globalSettings;

        private AmazonSimpleNotificationServiceClient _client = null;

        public AmazonSnsPushRegistrationService(
            IMetaDataRepository metaDataRepository,
            GlobalSettings globalSettings)
        {
            _metaDataRepository = metaDataRepository;
            _globalSettings = globalSettings;
            _client = new AmazonSimpleNotificationServiceClient(
                globalSettings.Amazon.AccessKeyId,
                globalSettings.Amazon.AccessKeySecret,
                RegionEndpoint.GetBySystemName(globalSettings.Amazon.Region));
        }

        public async Task CreateOrUpdateRegistrationAsync(string pushToken, string deviceId, string userId,
            string identifier, DeviceType type)
        {
            if(string.IsNullOrWhiteSpace(pushToken))
            {
                return;
            }

            var userData = string.Format("userId:{0},deviceId:{1}", userId, deviceId);
            if(!string.IsNullOrWhiteSpace(identifier))
            {
                userData = userData.Concat("deviceIdentifier:" + identifier).ToString();
            }

            var updateNeeded = false;
            var createNeeded = false;
            var endpointArn = await CreateEndpointAsync(type, pushToken, userData);

            try
            {
                var getResponse = await _client.GetEndpointAttributesAsync(new GetEndpointAttributesRequest
                {
                    EndpointArn = endpointArn
                });
                updateNeeded = getResponse.Attributes["Token"] != pushToken ||
                    !getResponse.Attributes["Enabled"].Equals("true", StringComparison.InvariantCultureIgnoreCase) ||
                    !getResponse.Attributes["CustomUserData"].Equals(userData);
            }
            catch(NotFoundException)
            {
                createNeeded = true;
            }

            if(createNeeded)
            {
                string userTopicArn = null;
                string identifierTopicArn = null;
                var createUserTopicResponse = await _client.CreateTopicAsync(new CreateTopicRequest
                {
                    Name = string.Concat("push__userId__", userId)
                });
                userTopicArn = createUserTopicResponse.TopicArn;
                // TODO: catch already created topic exception

                if(!string.IsNullOrWhiteSpace(identifier))
                {
                    var createIdentifierTopicResponse = await _client.CreateTopicAsync(new CreateTopicRequest
                    {
                        Name = string.Concat("push__deviceIdentifier__", identifier)
                    });
                    identifierTopicArn = createIdentifierTopicResponse.TopicArn;
                    // TODO: catch already created topic exception
                }

                endpointArn = await CreateEndpointAsync(type, pushToken, userData);
                await StoreDeviceEndpointArnAsync(deviceId, endpointArn);

                await _client.SubscribeAsync(new SubscribeRequest
                {
                    TopicArn = userTopicArn,
                    Endpoint = endpointArn
                });

                if(!string.IsNullOrWhiteSpace(identifierTopicArn))
                {
                    await _client.SubscribeAsync(new SubscribeRequest
                    {
                        TopicArn = identifierTopicArn,
                        Endpoint = endpointArn
                    });
                }
            }

            if(updateNeeded)
            {
                await _client.SetEndpointAttributesAsync(new SetEndpointAttributesRequest
                {
                    Attributes = new Dictionary<string, string>
                    {
                        ["Token"] = pushToken,
                        ["Enabled"] = "true",
                        ["CustomUserData"] = userData
                    },
                    EndpointArn = endpointArn
                });
            }
        }

        public async Task DeleteRegistrationAsync(string deviceId)
        {
            var endpointArn = await GetDeviceEndpointArnAsync(deviceId);
            await _client.DeleteEndpointAsync(new DeleteEndpointRequest
            {
                EndpointArn = endpointArn
            });
            // TODO: catch already deleted exception
        }

        public async Task AddUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId)
        {
            var createTopicResponse = await _client.CreateTopicAsync(new CreateTopicRequest
            {
                Name = string.Concat("push__organizationId__", organizationId)
            });
            // TODO: catch already created topic exception

            foreach(var id in deviceIds)
            {
                var endpointArn = await GetDeviceEndpointArnAsync(id);
                if(endpointArn == null)
                {
                    continue;
                }
                await _client.SubscribeAsync(new SubscribeRequest
                {
                    TopicArn = createTopicResponse.TopicArn,
                    Endpoint = endpointArn
                });
                // TODO: catch already subscribed exception
            }
        }

        public async Task DeleteUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId)
        {
            foreach(var id in deviceIds)
            {
                var endpointArn = await GetDeviceEndpointArnAsync(id);
                if(endpointArn == null)
                {
                    continue;
                }
                var subArn = CreateSubscriptionArn(string.Concat("push__organizationId__", organizationId),
                    endpointArn);
                if(subArn == null)
                {
                    continue;
                }
                await _client.UnsubscribeAsync(new UnsubscribeRequest
                {
                    SubscriptionArn = subArn
                });
                // TODO: catch already unsubscribed exception
            }
        }

        private string CreateTopicArn(string topicName)
        {
            return string.Format("arn:aws:sns:{0}:{1}:{2}",
                _globalSettings.Amazon.Region, _globalSettings.Amazon.AccountId, topicName);
        }

        private string CreateSubscriptionArn(string topicName, string endpointArn)
        {
            var endpointId = endpointArn?.Split('/').LastOrDefault();
            if(string.IsNullOrWhiteSpace(endpointId))
            {
                return null;
            }
            return string.Format("{0}:{1}", CreateTopicArn(topicName), endpointId);
        }

        private async Task<string> CreateEndpointAsync(DeviceType type, string pushToken, string userData)
        {
            string appArn = null;
            switch(type)
            {
                case DeviceType.Android:
                    appArn = _globalSettings.Sns.AndroidPlatformApplicationArn;
                    break;
                case DeviceType.iOS:
                    appArn = _globalSettings.Sns.IosPlatformApplicationArn;
                    break;
                default:
                    break;
            }
            if(string.IsNullOrWhiteSpace(appArn))
            {
                return null;
            }

            try
            {
                var createResponse = await _client.CreatePlatformEndpointAsync(new CreatePlatformEndpointRequest
                {
                    Token = pushToken,
                    PlatformApplicationArn = appArn,
                    CustomUserData = userData
                });
                return createResponse.EndpointArn;
            }
            catch(InvalidParameterException e)
            {
                var match = Regex.Match(e.Message,
                    ".*Endpoint (arn:aws:sns[^ ]+) already exists with the same [Tt]oken.*");
                if(!match.Success)
                {
                    throw;
                }
                return match.Groups[1].Value;
            }
        }

        private async Task StoreDeviceEndpointArnAsync(string deviceId, string endpointArn)
        {
            await _metaDataRepository.UpsertAsync("SnsDeviceEndpoint", deviceId,
                new Dictionary<string, string>
                {
                    ["EndpointArn"] = endpointArn
                });
        }

        private async Task<string> GetDeviceEndpointArnAsync(string deviceId)
        {
            var dict = await _metaDataRepository.GetAsync("SnsDeviceEndpoint", deviceId);
            return dict == null || !dict.ContainsKey("EndpointArn") ? null : dict["EndpointArn"];
        }
    }
}
