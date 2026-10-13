using Bit.Api.Dirt.Public.Models;
using Xunit;

namespace Bit.Api.Test.Dirt.Public.Models;

public class EventFilterRequestModelTests
{
    [Fact]
    public void ToDateRange_OnlyStartSupplied_DoesNotFallBackToThirtyDayDefault()
    {
        var suppliedStart = DateTime.UtcNow.AddDays(-3);
        var request = new EventFilterRequestModel { Start = suppliedStart };

        var dateRange = request.ToDateRange();

        Assert.Equal(suppliedStart, dateRange.Item1);
    }

    [Fact]
    public void ToDateRange_WritesResolvedBoundsBackOntoTheModelForDiagnosticLogging()
    {
        var request = new EventFilterRequestModel();

        var dateRange = request.ToDateRange();

        Assert.Equal(dateRange.Item1, request.Start);
        Assert.Equal(dateRange.Item2, request.End);
    }
}
