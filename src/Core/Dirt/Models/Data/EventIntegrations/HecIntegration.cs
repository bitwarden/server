namespace Bit.Core.Dirt.Models.Data.EventIntegrations;

public record HecIntegration(Uri Uri, string Scheme, string Token, string? Service = null);
