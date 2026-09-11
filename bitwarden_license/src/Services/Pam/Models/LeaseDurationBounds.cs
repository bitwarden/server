namespace Bit.Services.Pam.Models;

/// <summary>
/// Resolves the duration bounds for a single lease request, folding an access rule's optional
/// <c>DefaultLeaseDurationSeconds</c>/<c>MaxLeaseDurationSeconds</c> together with the global ceiling. Shared so
/// the pre-check (client picker) and submit command (enforcement) can't disagree.
/// </summary>
public static class LeaseDurationBounds
{
    /// <summary>
    /// The longest any single lease may run regardless of rule configuration (24h). A rule's own cap can only narrow
    /// this, never widen it.
    /// </summary>
    public const int GlobalMaxSeconds = 24 * 60 * 60;

    /// <summary>
    /// The default pre-fill duration if the rule stores none of its own (1h), clamped to the effective maximum.
    /// </summary>
    public const int GlobalDefaultSeconds = 60 * 60;

    /// <summary>
    /// The rule's cap, or the global ceiling, whichever is lower. A non-positive cap is treated as unset rather
    /// than as a rule that permits nothing.
    /// </summary>
    public static int EffectiveMax(int? ruleMaxSeconds) =>
        ruleMaxSeconds is > 0 ? Math.Min(ruleMaxSeconds.Value, GlobalMaxSeconds) : GlobalMaxSeconds;

    /// <summary>
    /// The rule's default, or the global default, clamped to <paramref name="effectiveMaxSeconds"/> so a narrower
    /// cap can't be handed a pre-fill value it forbids.
    /// </summary>
    public static int EffectiveDefault(int? ruleDefaultSeconds, int effectiveMaxSeconds) =>
        Math.Min(ruleDefaultSeconds is > 0 ? ruleDefaultSeconds.Value : GlobalDefaultSeconds, effectiveMaxSeconds);
}
