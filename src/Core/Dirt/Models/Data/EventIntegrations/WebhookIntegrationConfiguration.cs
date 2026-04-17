namespace Bit.Core.Dirt.Models.Data.EventIntegrations;

public record WebhookIntegrationConfiguration(Uri Uri, string? Scheme = null, string? Token = null);
