namespace Bit.Seeder.Options;

/// <summary>
/// Optional overrides applied on top of the organization's initial values.
/// Null properties mean "leave the value unchanged from <see cref="Bit.Seeder.Factories.OrganizationSeeder.Create"/>".
/// </summary>
public sealed record OrganizationOverrides
{
    public bool? UseAutomaticUserConfirmation { get; init; }
    public bool? AllowAdminAccessToAllCollectionItems { get; init; }
    public bool? LimitItemDeletion { get; init; }
    public bool? LimitCollectionCreation { get; init; }
    public bool? LimitCollectionDeletion { get; init; }
}
