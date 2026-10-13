using Bit.Seeder.Options;

namespace Bit.SeederUtility.Commands;

/// <summary>
/// Shared validation and mapping for the <c>--stripe-billing</c> / <c>--skip-trial</c> / <c>--trial-days</c>
/// trio, which the <c>organization</c> and <c>preset</c> commands expose identically.
/// </summary>
internal static class StripeBillingArgs
{
    private const int _minTrialDays = 1;
    private const int _maxTrialDays = 30;
    private const int _defaultTrialDays = 30;

    /// <summary>
    /// Rejects trial flags that are meaningless or contradictory. The day range mirrors production's
    /// <c>ValidateTrialLength</c> — <c>IOrganizationBillingService.Finalize</c> enforces nothing itself, so an
    /// out-of-range value would reach Stripe unchecked.
    /// </summary>
    internal static void Validate(bool stripeBilling, bool skipTrial, int? trialDays)
    {
        if (!stripeBilling && (skipTrial || trialDays.HasValue))
        {
            throw new ArgumentException(
                "--skip-trial and --trial-days only apply to Stripe billing. Add --stripe-billing or drop them.");
        }

        if (skipTrial && trialDays.HasValue)
        {
            throw new ArgumentException(
                "--skip-trial and --trial-days are mutually exclusive: one asks for no trial, the other for a trial.");
        }

        if (trialDays is < _minTrialDays or > _maxTrialDays)
        {
            throw new ArgumentException(
                $"--trial-days must be between {_minTrialDays} and {_maxTrialDays}.");
        }
    }

    /// <summary>
    /// Returns null when billing was not requested — the signal every seeding path reads as
    /// "make no Stripe calls".
    /// </summary>
    internal static StripeBillingOptions? ToOptions(bool stripeBilling, bool skipTrial, int? trialDays) =>
        stripeBilling
            ? new StripeBillingOptions { SkipTrial = skipTrial, TrialDays = trialDays ?? _defaultTrialDays }
            : null;
}
