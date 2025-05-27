using System.Text.Json.Nodes;
using Bit.Core.AdminConsole.Models.Data.Integrations;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public abstract class IntegrationEventHandlerBase(
    IUserRepository userRepository,
    IOrganizationRepository organizationRepository,
    IOrganizationIntegrationConfigurationRepository configurationRepository)
    : IEventMessageHandler
{
    public async Task HandleEventAsync(EventMessage eventMessage)
    {
        var organizationId = eventMessage.OrganizationId ?? Guid.Empty;
        var configurations = await configurationRepository.GetConfigurationDetailsAsync(
            organizationId,
            GetIntegrationType(),
            eventMessage.Type);

        foreach (var configuration in configurations)
        {
            var context = await BuildContextAsync(eventMessage, configuration.Template);
            var renderedTemplate = IntegrationTemplateProcessor.ReplaceTokens(configuration.Template, context);

            await ProcessEventIntegrationAsync(configuration.MergedConfiguration, renderedTemplate);
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

    protected abstract IntegrationType GetIntegrationType();

    protected abstract Task ProcessEventIntegrationAsync(JsonObject mergedConfiguration, string renderedTemplate);
}
