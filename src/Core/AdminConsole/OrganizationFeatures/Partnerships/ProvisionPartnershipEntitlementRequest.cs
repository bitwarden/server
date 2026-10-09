namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

public record ProvisionPartnershipEntitlementRequest
{
    public const int MaxMetadataKeys = 16;

    public required Guid OrganizationPartnershipId { get; init; }
    public required string ExternalId { get; init; }
    public IDictionary<string, string>? Metadata { get; init; }
    /// <summary>
    /// When the partner provisioned the customer, in UTC. Defaults to now.
    /// </summary>
    public DateTime? EffectiveAt { get; init; }
}
