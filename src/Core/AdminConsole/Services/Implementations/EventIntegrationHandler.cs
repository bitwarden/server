using System.Text.Json;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Integrations;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public class EventIntegrationHandler<T>(
    IntegrationType integrationType,
    IIntegrationPublisher integrationPublisher,
    IOrganizationIntegrationConfigurationRepository configurationRepository,
    IUserRepository userRepository,
    IOrganizationRepository organizationRepository)
    : IEventMessageHandler
{
    public async Task HandleEventAsync(EventMessage eventMessage)
    {
        var organizationId = eventMessage.OrganizationId ?? Guid.Empty;
        var configurations = await configurationRepository.GetConfigurationDetailsAsync(
            organizationId,
            integrationType,
            eventMessage.Type);

        foreach (var configuration in configurations)
        {
            var context = await BuildContextAsync(eventMessage, configuration.Template);
            var renderedTemplate = IntegrationTemplateProcessor.ReplaceTokens(configuration.Template, context);

            var config = configuration.MergedConfiguration.Deserialize<T>()
                         ?? throw new InvalidOperationException($"Failed to deserialize to {typeof(T).Name}");

            var message = new IntegrationMessage<T>
            {
                IntegrationType = integrationType,
                Configuration = config,
                RenderedTemplate = renderedTemplate,
                RetryCount = 0,
                NotBeforeUtc = null
            };

            await integrationPublisher.PublishAsync(message);
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
