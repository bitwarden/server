using Bit.Services.Pam.Models;
using Xunit;

namespace Bit.Services.Pam.Test.Models;

public class LeaseDurationBoundsTests
{
    [Theory]
    [InlineData(null, LeaseDurationBounds.GlobalMaxSeconds)] // no per-rule cap
    [InlineData(900, 900)]
    [InlineData(LeaseDurationBounds.GlobalMaxSeconds, LeaseDurationBounds.GlobalMaxSeconds)]
    [InlineData(7 * 24 * 60 * 60, 7 * 24 * 60 * 60)] // a multi-day rule cap survives intact
    // Narrowed only by the theoretical backstop, which no realistic rule reaches.
    [InlineData(LeaseDurationBounds.GlobalMaxSeconds + 1, LeaseDurationBounds.GlobalMaxSeconds)]
    [InlineData(0, LeaseDurationBounds.GlobalMaxSeconds)] // unset, not "permits nothing"
    [InlineData(-1, LeaseDurationBounds.GlobalMaxSeconds)]
    public void EffectiveMax_ResolvesTheRuleCapAgainstTheGlobalCeiling(int? ruleMaxSeconds, int expected)
    {
        Assert.Equal(expected, LeaseDurationBounds.EffectiveMax(ruleMaxSeconds));
    }

    [Theory]
    [InlineData(null, LeaseDurationBounds.GlobalMaxSeconds, LeaseDurationBounds.GlobalDefaultSeconds)]
    [InlineData(900, LeaseDurationBounds.GlobalMaxSeconds, 900)]
    [InlineData(0, LeaseDurationBounds.GlobalMaxSeconds, LeaseDurationBounds.GlobalDefaultSeconds)]
    [InlineData(-1, LeaseDurationBounds.GlobalMaxSeconds, LeaseDurationBounds.GlobalDefaultSeconds)]
    // A default above its own cap must not pre-fill above the cap.
    [InlineData(3600, 900, 900)]
    // A cap below the global default clamps it too, even with no rule default stored.
    [InlineData(null, 900, 900)]
    public void EffectiveDefault_IsClampedToTheEffectiveMax(
        int? ruleDefaultSeconds, int effectiveMaxSeconds, int expected)
    {
        Assert.Equal(expected, LeaseDurationBounds.EffectiveDefault(ruleDefaultSeconds, effectiveMaxSeconds));
    }
}
