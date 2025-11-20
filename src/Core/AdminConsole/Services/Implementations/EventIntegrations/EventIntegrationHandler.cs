using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Services;

public class EventIntegrationHandler<T>(
    IntegrationType integrationType,
    IEventIntegrationPublisher eventIntegrationPublisher,
    IIntegrationFilterService integrationFilterService,
    IIntegrationConfigurationDetailsCache configurationCache,
    IFusionCache fusionCache,
    IGroupRepository groupRepository,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    ILogger<EventIntegrationHandler<T>> logger)
    : IEventMessageHandler
{
    public async Task HandleEventAsync(EventMessage eventMessage)
    {
        if (eventMessage.OrganizationId is not Guid organizationId)
        {
            return;
        }

        var configurations = configurationCache.GetConfigurationDetails(
            organizationId,
            integrationType,
            eventMessage.Type);

        foreach (var configuration in configurations)
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
                    OrganizationId = organizationId.ToString(),
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
        var context = new IntegrationTemplateContext(eventMessage);

        if (IntegrationTemplateProcessor.TemplateRequiresGroup(template) && eventMessage.GroupId.HasValue)
        {
            context.Group = await fusionCache.GetOrSetAsync<Group?>(
                key: $"Group:{eventMessage.GroupId.Value:N}",
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
            context.Organization = await fusionCache.GetOrSetAsync<Organization?>(
                key: $"Organization:{organizationId:N}",
                factory: async _ => await organizationRepository.GetByIdAsync(organizationId)
            );
        }

        return context;
    }

    private async Task<OrganizationUserUserDetails?> GetUserFromCacheAsync(Guid organizationId, Guid userId) =>
        await fusionCache.GetOrSetAsync<OrganizationUserUserDetails?>(
            key: $"OrganizationUserUserDetails-orgId:{organizationId:N}-userId:{userId:N}",
            factory: async _ => await organizationUserRepository.GetDetailsByOrganizationIdUserIdAsync(
                organizationId: organizationId,
                userId: userId
            )
        );
}
