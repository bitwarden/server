namespace Bit.Core.Dirt.Models.Data.EventIntegrations;

public record WebhookIntegration(Uri Uri, string? Scheme = null, string? Token = null);
