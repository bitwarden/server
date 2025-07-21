#nullable enable

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public record WebhookIntegrationConfiguration(Uri Uri, string? Scheme = null, string? Token = null);
