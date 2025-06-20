#nullable enable

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public record WebhookIntegrationConfigurationDetails(string Url, string? Scheme = null, string? Token = null);
