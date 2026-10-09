namespace Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys;

public record CreateScopedOrganizationApiKeyRequest
{
    public required Guid OrganizationId { get; init; }
    public required string Name { get; init; }
    public required IEnumerable<string> Scopes { get; init; }
    public DateTime? ExpireAt { get; init; }
}
