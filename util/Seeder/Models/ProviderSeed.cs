using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Enums;
using Provider = Bit.Core.AdminConsole.Entities.Provider.Provider;

namespace Bit.Seeder.Models;

/// <summary>
/// Input for <see cref="Factories.ProviderSeeder.Create"/>. Identity and business details plus the
/// billing gateway configuration that used to be applied by a separate post-Create mutator.
/// </summary>
internal record ProviderSeed
{
    /// <summary>
    /// Mangled by the factory. NVARCHAR(50), stored HTML-encoded — read back via <see cref="Provider.DisplayName"/>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Derives the non-deliverable billing email. Not stored on the entity.
    /// </summary>
    public required string Domain { get; init; }

    /// <summary>
    /// Determines capabilities and billing model.
    /// </summary>
    public ProviderType Type { get; init; } = ProviderType.Msp;

    /// <summary>
    /// Defaults to Billable, the only status with a working billing surface.
    /// </summary>
    public ProviderStatusType Status { get; init; } = ProviderStatusType.Billable;

    /// <summary>
    /// False disables the provider and every organization it manages.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// The seeder writes no events; enabling this only sets the flag.
    /// </summary>
    public bool UseEvents { get; init; }

    /// <summary>
    /// Null keeps the factory default of Stripe.
    /// </summary>
    public GatewayType? Gateway { get; init; }

    /// <summary>
    /// Stripe <c>cus_…</c>. VARCHAR(50), filtered index, not validated against Stripe.
    /// </summary>
    public string? GatewayCustomerId { get; init; }

    /// <summary>
    /// Stripe <c>sub_…</c>. VARCHAR(50), filtered index, not validated against Stripe.
    /// </summary>
    public string? GatewaySubscriptionId { get; init; }

    /// <summary>
    /// NVARCHAR(50), HTML-encoded like <see cref="Name"/>. Read back via <see cref="Provider.DisplayBusinessName"/>.
    /// </summary>
    public string? BusinessName { get; init; }

    /// <summary>
    /// Two-letter ISO code. VARCHAR(2).
    /// </summary>
    public string? BusinessCountry { get; init; }

    /// <summary>
    /// NVARCHAR(50).
    /// </summary>
    public string? BillingPhone { get; init; }
}
