namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationScopedApiKeys;

public record CreateOrganizationScopedApiKeyRequest
{
    public required Guid OrganizationId { get; init; }
    public required string Name { get; init; }
    public required IEnumerable<string> Scopes { get; init; }
    public DateTime? ExpireAt { get; init; }
}
