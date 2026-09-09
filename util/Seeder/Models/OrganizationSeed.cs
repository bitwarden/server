using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Seeder.Options;

namespace Bit.Seeder.Models;

/// <summary>
/// Input for <see cref="Factories.OrganizationSeeder.Create"/>. Plan selection, key material, and the
/// billing and Secrets Manager configuration that used to be applied by separate post-Create mutators.
/// </summary>
internal record OrganizationSeed
{
    /// <summary>
    /// Mangled by the factory. NVARCHAR(50).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Mangled into <see cref="Organization.Identifier"/> and used to derive the billing email.
    /// </summary>
    public required string Domain { get; init; }

    /// <summary>
    /// Seat cap. For a plan-realistic value, compute it via <c>PlanFeatures.GenerateRealisticSeatCount</c>.
    /// </summary>
    public required int Seats { get; init; }

    /// <summary>
    /// Drives ~25 feature flags through <c>PlanFeatures.Apply</c>. Families rejects Secrets Manager.
    /// </summary>
    public PlanType PlanType { get; init; } = PlanType.EnterpriseAnnually;

    /// <summary>
    /// From <c>RustSdkService.GenerateOrganizationKeys()</c>. Null seeds an org that cannot share ciphers.
    /// </summary>
    public string? PublicKey { get; init; }

    /// <summary>
    /// Org private key, wrapped. Must pair with <see cref="PublicKey"/> from the same keypair.
    /// </summary>
    public string? PrivateKey { get; init; }

    /// <summary>
    /// Feature-flag overrides layered on top of the plan defaults. Null properties leave the plan value.
    /// </summary>
    public OrganizationOverrides? Overrides { get; init; }

    /// <summary>
    /// Null seeds an org with no billing relationship.
    /// </summary>
    public GatewayType? Gateway { get; init; }

    /// <summary>
    /// Stripe <c>cus_…</c>. VARCHAR(50), not validated against Stripe.
    /// </summary>
    public string? GatewayCustomerId { get; init; }

    /// <summary>
    /// Stripe <c>sub_…</c>. VARCHAR(50), not validated against Stripe.
    /// </summary>
    public string? GatewaySubscriptionId { get; init; }

    /// <summary>
    /// Throws for plans without a Secrets Manager tier (e.g. Families).
    /// </summary>
    public bool EnableSecretsManager { get; init; }

    /// <summary>
    /// Defaults to the plan's base seats: <see cref="Seats"/> for paid plans, 2 for Free.
    /// Ignored unless <see cref="EnableSecretsManager"/>.
    /// </summary>
    public int? SmSeats { get; init; }

    /// <summary>
    /// Defaults to the plan's base allotment: 50 for Enterprise and Teams-Annual, 20 for Teams, 3 for Free.
    /// Ignored unless <see cref="EnableSecretsManager"/>.
    /// </summary>
    public int? SmServiceAccounts { get; init; }
}
