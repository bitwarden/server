namespace Bit.Core.Dirt.Models.Data.EventIntegrations;

public record WebhookIntegrationConfigurationDetails(Uri Uri, string? Scheme = null, string? Token = null);
