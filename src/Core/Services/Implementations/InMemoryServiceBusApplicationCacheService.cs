using Azure.Messaging.ServiceBus;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Core.Services;

public class InMemoryServiceBusApplicationCacheService : InMemoryApplicationCacheService, IApplicationCacheService
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ServiceBusSender _topicMessageSender;
    private readonly string _subName;

    public InMemoryServiceBusApplicationCacheService(
        IOrganizationRepository organizationRepository,
        IProviderRepository providerRepository,
        GlobalSettings globalSettings)
        : base(organizationRepository, providerRepository)
    {
        _subName = CoreHelpers.GetApplicationCacheServiceBusSubscriptionName(globalSettings);
        _serviceBusClient = new ServiceBusClient(globalSettings.ServiceBus.ConnectionString);
        _topicMessageSender = new ServiceBusClient(globalSettings.ServiceBus.ConnectionString).CreateSender(globalSettings.ServiceBus.ApplicationCacheTopicName);
    }

    public override async Task UpsertOrganizationAbilityAsync(Organization organization)
    {
        await base.UpsertOrganizationAbilityAsync(organization);
        var message = new ServiceBusMessage
        {
            Subject = _subName,
            ApplicationProperties =
            {
                { "type", (byte)ApplicationCacheMessageType.UpsertOrganizationAbility },
                { "id", organization.Id },
            }
        };
        var task = _topicMessageSender.SendMessageAsync(message);
    }

    public override async Task DeleteOrganizationAbilityAsync(Guid organizationId)
    {
        await base.DeleteOrganizationAbilityAsync(organizationId);
        var message = new ServiceBusMessage
        {
            Subject = _subName,
            ApplicationProperties =
            {
                { "type", (byte)ApplicationCacheMessageType.DeleteOrganizationAbility },
                { "id", organizationId },
            }
        };
        var task = _topicMessageSender.SendMessageAsync(message);
    }

    public async Task BaseUpsertOrganizationAbilityAsync(Organization organization)
    {
        await base.UpsertOrganizationAbilityAsync(organization);
    }

    public async Task BaseDeleteOrganizationAbilityAsync(Guid organizationId)
    {
        await base.DeleteOrganizationAbilityAsync(organizationId);
    }

    public override async Task DeleteProviderAbilityAsync(Guid providerId)
    {
        await base.DeleteProviderAbilityAsync(providerId);
        var message = new ServiceBusMessage
        {
            Subject = _subName,
            ApplicationProperties =
            {
                { "type", (byte)ApplicationCacheMessageType.DeleteProviderAbility },
                { "id", providerId },
            }
        };
        var task = _topicMessageSender.SendMessageAsync(message);
    }
}
