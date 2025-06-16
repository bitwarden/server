#nullable enable

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public record WebhookIntegrationConfiguration(string? Scheme, string? Token, string Url);
