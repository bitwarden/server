namespace Bit.Seeder.Options;

/// <summary>
/// Optional overrides applied on top of plan defaults when creating an organization.
/// Null properties mean "keep the plan default".
/// </summary>
public sealed record OrganizationOverrides
{
    public bool? UseAutomaticUserConfirmation { get; init; }
    public bool? AllowAdminAccessToAllCollectionItems { get; init; }
    public bool? LimitItemDeletion { get; init; }
    public bool? LimitCollectionCreation { get; init; }
    public bool? LimitCollectionDeletion { get; init; }
}
