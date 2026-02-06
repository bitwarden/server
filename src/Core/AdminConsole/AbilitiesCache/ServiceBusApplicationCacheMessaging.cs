using Azure.Messaging.ServiceBus;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.AbilitiesCache;

public class ServiceBusApplicationCacheMessaging : IApplicationCacheServiceBusMessaging
{
    private readonly ServiceBusSender _topicMessageSender;
    private readonly string _subName;

    public ServiceBusApplicationCacheMessaging(
        GlobalSettings globalSettings)
    {
        _subName = CoreHelpers.GetApplicationCacheServiceBusSubscriptionName(globalSettings);
        var serviceBusClient = new ServiceBusClient(globalSettings.ServiceBus.ConnectionString);
        _topicMessageSender = serviceBusClient.CreateSender(globalSettings.ServiceBus.ApplicationCacheTopicName);
    }

    public async Task NotifyOrganizationAbilityUpsertedAsync(Organization organization)
    {
        var message = new ServiceBusMessage
        {
            Subject = _subName,
            ApplicationProperties =
            {
                { "type", (byte)ApplicationCacheMessageType.UpsertOrganizationAbility },
                { "id", organization.Id },
            }
        };
        await _topicMessageSender.SendMessageAsync(message);
    }

    public async Task NotifyOrganizationAbilityDeletedAsync(Guid organizationId)
    {
        var message = new ServiceBusMessage
        {
            Subject = _subName,
            ApplicationProperties =
            {
                { "type", (byte)ApplicationCacheMessageType.DeleteOrganizationAbility },
                { "id", organizationId },
            }
        };
        await _topicMessageSender.SendMessageAsync(message);
    }

    public async Task NotifyProviderAbilityDeletedAsync(Guid providerId)
    {
        var message = new ServiceBusMessage
        {
            Subject = _subName,
            ApplicationProperties =
            {
                { "type", (byte)ApplicationCacheMessageType.DeleteProviderAbility },
                { "id", providerId },
            }
        };
        await _topicMessageSender.SendMessageAsync(message);
    }
}
