namespace Bit.Services.Pam.Models;

/// <summary>
/// Resolves the duration bounds that apply to a single lease request, folding an access rule's optional
/// <c>DefaultLeaseDurationSeconds</c>/<c>MaxLeaseDurationSeconds</c> together with the global ceiling. One home for the
/// arithmetic, because two callers have to agree on it exactly: the pre-check publishes the bounds so the client can
/// shape its duration picker, and the submit command enforces them. A client narrowing its picker to a cap the server
/// does not enforce (or the reverse) is how a rule's configured maximum came to be ignored in the first place.
/// </summary>
public static class LeaseDurationBounds
{
    /// <summary>
    /// The longest any single lease may run regardless of rule configuration (24h). A rule's own cap can only narrow
    /// this, never widen it.
    /// </summary>
    public const int GlobalMaxSeconds = 24 * 60 * 60;

    /// <summary>
    /// The duration a request pre-fills with when its rule stores no default of its own (1h). Always clamped to the
    /// effective maximum, so a rule that caps below this never pre-fills an over-cap value.
    /// </summary>
    public const int GlobalDefaultSeconds = 60 * 60;

    /// <summary>
    /// The effective ceiling for a request: the rule's cap when it sets one, otherwise the global ceiling. A rule cap
    /// above the global ceiling is narrowed to it; a non-positive cap is treated as unset rather than as a rule that
    /// permits nothing, since that would deny every request under it.
    /// </summary>
    public static int EffectiveMax(int? ruleMaxSeconds) =>
        ruleMaxSeconds is > 0 ? Math.Min(ruleMaxSeconds.Value, GlobalMaxSeconds) : GlobalMaxSeconds;

    /// <summary>
    /// The effective pre-fill duration: the rule's default when it sets one, otherwise the global default — either way
    /// clamped to <paramref name="effectiveMaxSeconds"/>. The clamp is what keeps a rule configured
    /// "default 1h, maximum 15m" from handing the client a pre-filled value its own cap forbids.
    /// </summary>
    public static int EffectiveDefault(int? ruleDefaultSeconds, int effectiveMaxSeconds) =>
        Math.Min(ruleDefaultSeconds is > 0 ? ruleDefaultSeconds.Value : GlobalDefaultSeconds, effectiveMaxSeconds);
}
