using Bit.Seeder.Options;

namespace Bit.SeederUtility.Commands;

/// <summary>
/// Shared validation and mapping for the Stripe billing flags: <c>--stripe-billing</c>, <c>--stripe-card</c>,
/// and <c>--stripe-address</c> on every billing command, plus <c>--skip-trial</c> / <c>--trial-days</c> on
/// the <c>organization</c> and <c>preset</c> commands.
/// </summary>
internal static class StripeBillingArgs
{
    /// <summary>
    /// Rejects trial flags that are meaningless or contradictory, then the card and address flags.
    /// </summary>
    internal static void Validate(bool stripeBilling, bool skipTrial, int? trialDays, string? card, string? address)
    {
        if (!stripeBilling && (skipTrial || trialDays.HasValue))
        {
            throw new ArgumentException(
                "--skip-trial and --trial-days only apply to Stripe billing. Add --stripe-billing or drop them.");
        }

        StripeBillingOptions.ValidateTrial(skipTrial, trialDays, "--skip-trial", "--trial-days");

        ValidateProfile(stripeBilling, card, address);
    }

    /// <summary>
    /// Rejects <c>--stripe-card</c> / <c>--stripe-address</c> without <c>--stripe-billing</c>, and names the
    /// Seeder's catalog doesn't know.
    /// </summary>
    internal static void ValidateProfile(bool stripeBilling, string? card, string? address)
    {
        if (!stripeBilling && (!string.IsNullOrEmpty(card) || !string.IsNullOrEmpty(address)))
        {
            throw new ArgumentException(
                "--stripe-card and --stripe-address only apply to Stripe billing. Add --stripe-billing or drop them.");
        }

        StripeBillingProfile.Parse(card, address);
    }

    /// <summary>
    /// Returns null when billing was not requested — the signal every seeding path reads as
    /// "make no Stripe calls".
    /// </summary>
    internal static StripeBillingOptions? ToOptions(
        bool stripeBilling, bool skipTrial, int? trialDays, string? card, string? address) =>
        stripeBilling
            ? new StripeBillingOptions
            {
                SkipTrial = skipTrial,
                TrialDays = trialDays ?? StripeBillingOptions.DefaultTrialDays,
                Profile = StripeBillingProfile.Parse(card, address),
            }
            : null;

    /// <summary>
    /// Returns null when billing was not requested — the signal every seeding path reads as
    /// "make no Stripe calls".
    /// </summary>
    internal static StripeBillingProfile? ToProfile(bool stripeBilling, string? card, string? address) =>
        stripeBilling ? StripeBillingProfile.Parse(card, address) : null;
}
