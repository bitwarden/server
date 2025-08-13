#nullable enable

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public record HecIntegration(Uri Uri, string Scheme, string Token, string? Service = null);
