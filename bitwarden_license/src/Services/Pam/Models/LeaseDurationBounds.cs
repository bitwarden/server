namespace Bit.Services.Pam.Models;

/// <summary>
/// Resolves a lease's duration bounds from an access rule's optional default and cap and the global ceiling.
/// </summary>
public static class LeaseDurationBounds
{
    /// <summary>
    /// The longest any single lease may run, whatever the rule says (one year).
    /// </summary>
    public const int GlobalMaxSeconds = 365 * 24 * 60 * 60;

    /// <summary>
    /// The default duration when the rule stores none (1h).
    /// </summary>
    public const int GlobalDefaultSeconds = 60 * 60;

    /// <summary>
    /// The rule's cap or the global ceiling, whichever is lower. A non-positive cap counts as unset.
    /// </summary>
    public static int EffectiveMax(int? ruleMaxSeconds) =>
        ruleMaxSeconds is > 0 ? Math.Min(ruleMaxSeconds.Value, GlobalMaxSeconds) : GlobalMaxSeconds;

    /// <summary>
    /// The rule's default, or the global one, clamped to <paramref name="effectiveMaxSeconds"/>.
    /// </summary>
    public static int EffectiveDefault(int? ruleDefaultSeconds, int effectiveMaxSeconds) =>
        Math.Min(ruleDefaultSeconds is > 0 ? ruleDefaultSeconds.Value : GlobalDefaultSeconds, effectiveMaxSeconds);
}
