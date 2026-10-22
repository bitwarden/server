using Bit.Api.Models.Response;
using Bit.Core.Test.Billing.Mocks.Plans;
using Xunit;

namespace Bit.Api.Test.Models.Response;

public class PlanResponseModelTests
{
    [Theory]
    [InlineData(true, "privileged-controls-enterprise-seat-annually", 72)]
    [InlineData(false, "privileged-controls-enterprise-seat-monthly", 6)]
    public void Constructor_PlanWithPrivilegedControls_MapsPrivilegedControls(
        bool isAnnual,
        string expectedStripeSeatPlanId,
        decimal expectedSeatPrice)
    {
        var model = new PlanResponseModel(new EnterprisePlan(isAnnual));

        Assert.NotNull(model.PrivilegedControls);
        Assert.Equal(expectedStripeSeatPlanId, model.PrivilegedControls.StripeSeatPlanId);
        Assert.Equal(expectedSeatPrice, model.PrivilegedControls.SeatPrice);
        Assert.Equal(10, model.PrivilegedControls.DefaultSeatMinimum);
    }

    [Fact]
    public void Constructor_PlanWithPrivilegedControls_MapsBaseSeats()
    {
        var model = new PlanResponseModel(new EnterprisePlanWithBaseSeats());

        Assert.Equal(3, model.PrivilegedControls.BaseSeats);
    }

    // The shared mock sets BaseSeats to 0, which is the default, so it can't prove the field is mapped.
    private sealed record EnterprisePlanWithBaseSeats : EnterprisePlan
    {
        public EnterprisePlanWithBaseSeats() : base(isAnnual: true) =>
            PrivilegedControls = PrivilegedControls with { BaseSeats = 3 };
    }

    [Fact]
    public void Constructor_PlanWithoutPrivilegedControls_LeavesPrivilegedControlsNull()
    {
        var model = new PlanResponseModel(new TeamsPlan(isAnnual: true));

        Assert.Null(model.PrivilegedControls);
    }
}
