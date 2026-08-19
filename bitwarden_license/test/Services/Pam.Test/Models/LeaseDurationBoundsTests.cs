using Bit.Services.Pam.Models;
using Xunit;

namespace Bit.Services.Pam.Test.Models;

public class LeaseDurationBoundsTests
{
    [Theory]
    [InlineData(null, LeaseDurationBounds.GlobalMaxSeconds)] // no per-rule cap
    [InlineData(900, 900)]
    [InlineData(LeaseDurationBounds.GlobalMaxSeconds, LeaseDurationBounds.GlobalMaxSeconds)]
    [InlineData(7 * 24 * 60 * 60, LeaseDurationBounds.GlobalMaxSeconds)] // narrowed to the global ceiling
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
    // PM-39858's shape: a rule left at a 1h default but capped at 15m must not pre-fill 1h.
    [InlineData(3600, 900, 900)]
    // A cap below the global default clamps it too, even with no rule default stored.
    [InlineData(null, 900, 900)]
    public void EffectiveDefault_IsClampedToTheEffectiveMax(
        int? ruleDefaultSeconds, int effectiveMaxSeconds, int expected)
    {
        Assert.Equal(expected, LeaseDurationBounds.EffectiveDefault(ruleDefaultSeconds, effectiveMaxSeconds));
    }
}
