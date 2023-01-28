using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Azure.ServiceBus;

namespace Bit.Core.Services;

public class InMemoryServiceBusApplicationCacheService : InMemoryApplicationCacheService, IApplicationCacheService
{
    private readonly TopicClient _topicClient;
    private readonly string _subName;

    public InMemoryServiceBusApplicationCacheService(
        IOrganizationRepository organizationRepository,
        IProviderRepository providerRepository,
        GlobalSettings globalSettings)
        : base(organizationRepository, providerRepository)
    {
        _subName = CoreHelpers.GetApplicationCacheServiceBusSubcriptionName(globalSettings);
        _topicClient = new TopicClient(globalSettings.ServiceBus.ConnectionString,
            globalSettings.ServiceBus.ApplicationCacheTopicName);
    }

    public override async Task UpsertOrganizationAbilityAsync(Organization organization)
    {
        await base.UpsertOrganizationAbilityAsync(organization);
        var message = new Message
        {
            Label = _subName,
            UserProperties =
            {
                { "type", (byte)ApplicationCacheMessageType.UpsertOrganizationAbility },
                { "id", organization.Id },
            }
        };
        var task = _topicClient.SendAsync(message);
    }

    public override async Task DeleteOrganizationAbilityAsync(Guid organizationId)
    {
        await base.DeleteOrganizationAbilityAsync(organizationId);
        var message = new Message
        {
            Label = _subName,
            UserProperties =
            {
                { "type", (byte)ApplicationCacheMessageType.DeleteOrganizationAbility },
                { "id", organizationId },
            }
        };
        var task = _topicClient.SendAsync(message);
    }

    public async Task BaseUpsertOrganizationAbilityAsync(Organization organization)
    {
        await base.UpsertOrganizationAbilityAsync(organization);
    }

    public async Task BaseDeleteOrganizationAbilityAsync(Guid organizationId)
    {
        await base.DeleteOrganizationAbilityAsync(organizationId);
    }
}
