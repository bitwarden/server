using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Models.StaticStore;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Helpers;

public class PrivilegedControlsSeatMinimumResolverTests
{
    private const int DefaultSeatMinimum = 10;
    private const int PromotionalSeatMinimum = 5;

    [Fact]
    public void Resolve_WithAnApprovedPromotionalMinimum_ReturnsIt()
    {
        var result = PrivilegedControlsSeatMinimumResolver.Resolve(PrivilegedControls(), PromotionalSeatMinimum);

        Assert.Equal(PromotionalSeatMinimum, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(PromotionalSeatMinimum - 1)]
    public void Resolve_WithoutAnApprovedPromotionalMinimum_ReturnsTheDefault(int? requestedSeatMinimum)
    {
        var result = PrivilegedControlsSeatMinimumResolver.Resolve(PrivilegedControls(), requestedSeatMinimum);

        Assert.Equal(DefaultSeatMinimum, result);
    }

    private static Plan.PrivilegedControlsPlanFeatures PrivilegedControls() => new()
    {
        StripeSeatPlanId = "pam-seat",
        DefaultSeatMinimum = DefaultSeatMinimum,
        PromotionalSeatMinimums = [PromotionalSeatMinimum]
    };
}
