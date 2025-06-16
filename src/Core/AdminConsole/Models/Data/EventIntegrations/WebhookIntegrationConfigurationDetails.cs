#nullable enable

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public record WebhookIntegrationConfigurationDetails(string? Scheme, string? Token, string Url);
