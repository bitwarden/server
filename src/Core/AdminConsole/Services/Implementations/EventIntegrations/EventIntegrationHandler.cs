#nullable enable

using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public class EventIntegrationHandler<T>(
    IntegrationType integrationType,
    IEventIntegrationPublisher eventIntegrationPublisher,
    IIntegrationFilterService integrationFilterService,
    IIntegrationConfigurationDetailsCache configurationDetailsCache,
    IUserRepository userRepository,
    IOrganizationRepository organizationRepository)
    : IEventMessageHandler
{
    public async Task HandleEventAsync(EventMessage eventMessage)
    {
        if (eventMessage.OrganizationId is not Guid organizationId)
        {
            return;
        }

        var configurations = await configurationDetailsCache.GetOrAddAsync<T>(
            organizationId,
            integrationType,
            eventMessage.Type);

        foreach (var configuration in configurations)
        {
            // Evaluate filters - if false, then discard and do not process
            if (configuration.FilterGroup is IntegrationFilterGroup filters &&
                !integrationFilterService.EvaluateFilterGroup(filters, eventMessage))
            {
                continue;
            }

            // Valid filter - assemble message and publish to Integration topic/exchange
            var context = await BuildContextAsync(eventMessage, configuration.Template);
            var renderedTemplate = IntegrationTemplateProcessor.ReplaceTokens(configuration.Template, context);
            var messageId = eventMessage.IdempotencyId ?? Guid.NewGuid();
            var message = new IntegrationMessage<T>
            {
                IntegrationType = integrationType,
                MessageId = messageId.ToString(),
                Configuration = configuration.Configuration,
                RenderedTemplate = renderedTemplate,
                RetryCount = 0,
                DelayUntilDate = null
            };

            await eventIntegrationPublisher.PublishAsync(message);
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

        if (IntegrationTemplateProcessor.TemplateRequiresUser(template) && eventMessage.UserId.HasValue)
        {
            context.User = await userRepository.GetByIdAsync(eventMessage.UserId.Value);
        }

        if (IntegrationTemplateProcessor.TemplateRequiresActingUser(template) && eventMessage.ActingUserId.HasValue)
        {
            context.ActingUser = await userRepository.GetByIdAsync(eventMessage.ActingUserId.Value);
        }

        if (IntegrationTemplateProcessor.TemplateRequiresOrganization(template) && eventMessage.OrganizationId.HasValue)
        {
            context.Organization = await organizationRepository.GetByIdAsync(eventMessage.OrganizationId.Value);
        }

        return context;
    }
}
