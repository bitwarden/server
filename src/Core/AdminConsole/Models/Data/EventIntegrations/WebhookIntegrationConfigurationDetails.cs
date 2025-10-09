namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public record WebhookIntegrationConfigurationDetails(Uri Uri, string? Scheme = null, string? Token = null);
