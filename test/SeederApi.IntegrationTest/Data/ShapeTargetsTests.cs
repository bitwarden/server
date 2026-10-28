using Bit.Seeder.Data.Distributions;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Data;

public class ShapeTargetsTests
{
    [Fact]
    public void EnsureReached_ShortByExactlyTolerance_DoesNotThrow()
    {
        ShapeTargets.EnsureReached("rows", 99, 100);
    }

    [Fact]
    public void EnsureReached_ShortByMoreThanTolerance_ThrowsNamingTarget()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ShapeTargets.EnsureReached("rows", 98, 100));

        Assert.Contains("but the preset asks for 100", ex.Message);
    }
}
