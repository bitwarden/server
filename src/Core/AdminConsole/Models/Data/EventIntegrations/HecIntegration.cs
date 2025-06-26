#nullable enable

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public record HecIntegration(string Scheme, string Token, Uri Uri);
