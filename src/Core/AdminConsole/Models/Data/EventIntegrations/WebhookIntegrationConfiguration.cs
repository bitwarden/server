#nullable enable

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public record WebhookIntegrationConfiguration(string Url, string? Scheme = null, string? Token = null);
