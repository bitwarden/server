using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Services;

public class EventIntegrationHandler<T>(
    IntegrationType integrationType,
    IEventIntegrationPublisher eventIntegrationPublisher,
    IIntegrationFilterService integrationFilterService,
    IFusionCache cache,
    IOrganizationIntegrationConfigurationRepository configurationRepository,
    IGroupRepository groupRepository,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    ILogger<EventIntegrationHandler<T>> logger)
    : IEventMessageHandler
{
    public async Task HandleEventAsync(EventMessage eventMessage)
    {
        foreach (var configuration in await GetConfigurationDetailsListAsync(eventMessage))
        {
            try
            {
                if (configuration.Filters is string filterJson)
                {
                    // Evaluate filters - if false, then discard and do not process
                    var filters = JsonSerializer.Deserialize<IntegrationFilterGroup>(filterJson)
                        ?? throw new InvalidOperationException($"Failed to deserialize Filters to FilterGroup");
                    if (!integrationFilterService.EvaluateFilterGroup(filters, eventMessage))
                    {
                        continue;
                    }
                }

                // Valid filter - assemble message and publish to Integration topic/exchange
                var template = configuration.Template ?? string.Empty;
                var context = await BuildContextAsync(eventMessage, template);
                var renderedTemplate = IntegrationTemplateProcessor.ReplaceTokens(template, context);
                var messageId = eventMessage.IdempotencyId ?? Guid.NewGuid();
                var config = configuration.MergedConfiguration.Deserialize<T>()
                    ?? throw new InvalidOperationException($"Failed to deserialize to {typeof(T).Name} - bad Configuration");

                var message = new IntegrationMessage<T>
                {
                    IntegrationType = integrationType,
                    MessageId = messageId.ToString(),
                    OrganizationId = eventMessage.OrganizationId?.ToString(),
                    Configuration = config,
                    RenderedTemplate = renderedTemplate,
                    RetryCount = 0,
                    DelayUntilDate = null
                };

                await eventIntegrationPublisher.PublishAsync(message);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to publish Integration Message for {Type}, check Id {RecordId} for error in Configuration or Filters",
                    typeof(T).Name,
                    configuration.Id);
            }
        }
    }

    public async Task HandleManyEventsAsync(IEnumerable<EventMessage> eventMessages)
    {
        foreach (var eventMessage in eventMessages)
        {
            await HandleEventAsync(eventMessage);
        }
    }

    internal async Task<IntegrationTemplateContext> BuildContextAsync(EventMessage eventMessage, string template)
    {
        // Note: All of these cache calls use the default options, including TTL of 30 minutes

        var context = new IntegrationTemplateContext(eventMessage);

        if (IntegrationTemplateProcessor.TemplateRequiresGroup(template) && eventMessage.GroupId.HasValue)
        {
            context.Group = await cache.GetOrSetAsync<Group?>(
                key: EventIntegrationsCacheConstants.BuildCacheKeyForGroup(eventMessage.GroupId.Value),
                factory: async _ => await groupRepository.GetByIdAsync(eventMessage.GroupId.Value)
            );
        }

        if (eventMessage.OrganizationId is not Guid organizationId)
        {
            return context;
        }

        if (IntegrationTemplateProcessor.TemplateRequiresUser(template) && eventMessage.UserId.HasValue)
        {
            context.User = await GetUserFromCacheAsync(organizationId, eventMessage.UserId.Value);
        }

        if (IntegrationTemplateProcessor.TemplateRequiresActingUser(template) && eventMessage.ActingUserId.HasValue)
        {
            context.ActingUser = await GetUserFromCacheAsync(organizationId, eventMessage.ActingUserId.Value);
        }

        if (IntegrationTemplateProcessor.TemplateRequiresOrganization(template))
        {
            context.Organization = await cache.GetOrSetAsync<Organization?>(
                key: EventIntegrationsCacheConstants.BuildCacheKeyForOrganization(organizationId),
                factory: async _ => await organizationRepository.GetByIdAsync(organizationId)
            );
        }

        return context;
    }

    private async Task<List<OrganizationIntegrationConfigurationDetails>> GetConfigurationDetailsListAsync(EventMessage eventMessage)
    {
        if (eventMessage.OrganizationId is not Guid organizationId)
        {
            return [];
        }

        List<OrganizationIntegrationConfigurationDetails> configurations = [];

        var integrationTag = EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
            organizationId,
            integrationType
        );

        configurations.AddRange(await cache.GetOrSetAsync<List<OrganizationIntegrationConfigurationDetails>>(
            key: EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
                organizationId: organizationId,
                integrationType: integrationType,
                eventType: eventMessage.Type),
            factory: async _ => await configurationRepository.GetConfigurationDetailsAsync(
                organizationId: organizationId,
                integrationType: integrationType,
                eventType: eventMessage.Type
            ),
            options: new FusionCacheEntryOptions(
                duration: EventIntegrationsCacheConstants.DurationForOrganizationIntegrationConfigurationDetails),
            tags: [integrationTag]
        ));
        configurations.AddRange(await cache.GetOrSetAsync<List<OrganizationIntegrationConfigurationDetails>>(
            key: EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
                organizationId: organizationId,
                integrationType: integrationType,
                eventType: null),
            factory: async _ => await configurationRepository.GetManyConfigurationDetailsByOrganizationIdIntegrationTypeAsync(
                organizationId,
                integrationType
            ),
            options: new FusionCacheEntryOptions(
                duration: EventIntegrationsCacheConstants.DurationForOrganizationIntegrationConfigurationDetails),
            tags: [integrationTag]
        ));

        return configurations;
    }

    private async Task<OrganizationUserUserDetails?> GetUserFromCacheAsync(Guid organizationId, Guid userId) =>
        await cache.GetOrSetAsync<OrganizationUserUserDetails?>(
            key: EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationUser(organizationId, userId),
            factory: async _ => await organizationUserRepository.GetDetailsByOrganizationIdUserIdAsync(
                organizationId: organizationId,
                userId: userId
            )
        );
}
