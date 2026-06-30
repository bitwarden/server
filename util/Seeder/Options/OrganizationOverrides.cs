namespace Bit.Seeder.Options;

/// <summary>
/// Optional overrides applied on top of the organization's initial values.
/// Null properties mean "leave the value unchanged from <see cref="Bit.Seeder.Factories.OrganizationSeeder.Create"/>".
/// </summary>
public sealed record OrganizationOverrides
{
    // Collection management settings.
    public bool? UseAutomaticUserConfirmation { get; init; }
    public bool? AllowAdminAccessToAllCollectionItems { get; init; }
    public bool? LimitItemDeletion { get; init; }
    public bool? LimitCollectionCreation { get; init; }
    public bool? LimitCollectionDeletion { get; init; }

    // Capability flags. Set Secrets Manager via PlanFeatures.EnableSecretsManager, not here.
    public bool? UseGroups { get; init; }
    public bool? UsePolicies { get; init; }
    public bool? UseSso { get; init; }
    public bool? UseKeyConnector { get; init; }
    public bool? UseScim { get; init; }
    public bool? UseDirectory { get; init; }
    public bool? UseEvents { get; init; }
    public bool? UseTotp { get; init; }
    public bool? Use2fa { get; init; }
    public bool? UseApi { get; init; }
    public bool? UseResetPassword { get; init; }
    public bool? UseCustomPermissions { get; init; }
    public bool? UseOrganizationDomains { get; init; }
    public bool? UsersGetPremium { get; init; }
    public bool? SelfHost { get; init; }
    public bool? UseRiskInsights { get; init; }
    public bool? UseMyItems { get; init; }
    public bool? UseAdminSponsoredFamilies { get; init; }
    public bool? UseInviteLinks { get; init; }
    public bool? SyncSeats { get; init; }
    public bool? UsePasswordManager { get; init; }
}
