namespace Bit.Seeder.Options;

/// <summary>
/// Opt-in configuration for creating real Stripe test-environment billing alongside a seeded organization.
/// </summary>
/// <remarks>
/// The presence of this record <em>is</em> the opt-in: a null <c>StripeBillingOptions</c> anywhere it is
/// threaded means "make no Stripe calls at all", which is the default for every seeding path.
/// </remarks>
public sealed record StripeBillingOptions
{
    /// <summary>
    /// When true the subscription is created without a trial, so Stripe charges the test card immediately
    /// and the subscription lands in <c>active</c> rather than <c>trialing</c>.
    /// </summary>
    public bool SkipTrial { get; init; }

    /// <summary>
    /// Trial length in days. Ignored when <see cref="SkipTrial"/> is set.
    /// Callers validate the 1–30 range, mirroring production's <c>ValidateTrialLength</c> —
    /// <c>IOrganizationBillingService.Finalize</c> does not.
    /// </summary>
    public int TrialDays { get; init; } = 30;
}
