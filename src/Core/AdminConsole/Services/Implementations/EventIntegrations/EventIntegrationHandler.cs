#nullable enable

using System.Text.Json;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public class EventIntegrationHandler<T>(
    IntegrationType integrationType,
    IEventIntegrationPublisher eventIntegrationPublisher,
    IOrganizationIntegrationConfigurationRepository configurationRepository,
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

        var configurations = await configurationRepository.GetConfigurationDetailsAsync(
            organizationId,
            integrationType,
            eventMessage.Type);

        foreach (var configuration in configurations)
        {
            var template = configuration.Template ?? string.Empty;
            var context = await BuildContextAsync(eventMessage, template);
            var renderedTemplate = IntegrationTemplateProcessor.ReplaceTokens(template, context);
            var messageId = eventMessage.IdempotencyId ?? Guid.NewGuid();

            var config = configuration.MergedConfiguration.Deserialize<T>()
                         ?? throw new InvalidOperationException($"Failed to deserialize to {typeof(T).Name}");

            var message = new IntegrationMessage<T>
            {
                IntegrationType = integrationType,
                MessageId = messageId.ToString(),
                Configuration = config,
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
