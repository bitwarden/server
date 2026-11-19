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
    /// The trial-length bounds <see cref="ValidateTrial"/> enforces, and the length used when a caller sets none.
    /// </summary>
    public const int MinTrialDays = 1, MaxTrialDays = 30, DefaultTrialDays = 30;

    /// <summary>
    /// When true the subscription is created without a trial, so Stripe charges the test card immediately
    /// and the subscription lands in <c>active</c> rather than <c>trialing</c>.
    /// </summary>
    public bool SkipTrial { get; init; }

    /// <summary>
    /// Trial length in days. Ignored when <see cref="SkipTrial"/> is set.
    /// Callers validate it with <see cref="ValidateTrial"/>.
    /// </summary>
    public int TrialDays { get; init; } = DefaultTrialDays;

    /// <summary>
    /// The test card and billing address. Defaults to Visa at a US address.
    /// </summary>
    public StripeBillingProfile Profile { get; init; } = new();

    /// <summary>
    /// Rejects a request for both no trial and a trial, and a trial length outside
    /// <see cref="MinTrialDays"/>–<see cref="MaxTrialDays"/>. The range mirrors production's
    /// <c>ValidateTrialLength</c> — <c>IOrganizationBillingService.Finalize</c> enforces nothing itself, so an
    /// out-of-range value would reach Stripe unchecked.
    /// </summary>
    /// <param name="skipTrial">Whether the caller asked for no trial.</param>
    /// <param name="trialDays">The requested trial length, or null for the default.</param>
    /// <param name="skipTrialName">How the caller names the skip-trial argument in error messages.</param>
    /// <param name="trialDaysName">How the caller names the trial-days argument in error messages.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the two arguments conflict or the length is out of range.
    /// </exception>
    public static void ValidateTrial(bool skipTrial, int? trialDays, string skipTrialName, string trialDaysName)
    {
        if (skipTrial && trialDays.HasValue)
        {
            throw new ArgumentException(
                $"{skipTrialName} and {trialDaysName} are mutually exclusive: one asks for no trial, " +
                "the other for a trial.");
        }

        if (trialDays is < MinTrialDays or > MaxTrialDays)
        {
            throw new ArgumentException($"{trialDaysName} must be between {MinTrialDays} and {MaxTrialDays}.");
        }
    }
}
