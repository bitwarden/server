﻿using System.Text.Json;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class EventIntegrationHandler<T>(
    IntegrationType integrationType,
    IEventIntegrationPublisher eventIntegrationPublisher,
    IIntegrationFilterService integrationFilterService,
    IIntegrationConfigurationDetailsCache configurationCache,
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

    private async Task<IntegrationTemplateContext> BuildContextAsync(EventMessage eventMessage, string template)
    {
        var context = new IntegrationTemplateContext(eventMessage);

        if (IntegrationTemplateProcessor.TemplateRequiresGroup(template) && eventMessage.GroupId.HasValue)
        {
            context.Group = await groupRepository.GetByIdAsync(eventMessage.GroupId.Value);
        }

        if (eventMessage.OrganizationId is not Guid organizationId)
        {
            return context;
        }

        if (IntegrationTemplateProcessor.TemplateRequiresUser(template) && eventMessage.UserId.HasValue)
        {
            var orgUser = await organizationUserRepository.GetByOrganizationAsync(organizationId: organizationId, userId: eventMessage.UserId.Value);
            if (orgUser is not null)
            {
                context.User = await organizationUserRepository.GetDetailsByIdAsync(orgUser.Id);
            }
        }

        if (IntegrationTemplateProcessor.TemplateRequiresActingUser(template) && eventMessage.ActingUserId.HasValue)
        {
            var orgUser = await organizationUserRepository.GetByOrganizationAsync(organizationId: organizationId, userId: eventMessage.ActingUserId.Value);
            if (orgUser is not null)
            {
                context.ActingUser = await organizationUserRepository.GetDetailsByIdAsync(orgUser.Id);
            }
        }

        if (IntegrationTemplateProcessor.TemplateRequiresOrganization(template))
        {
            context.Organization = await organizationRepository.GetByIdAsync(organizationId);
        }

        return context;
    }
}
