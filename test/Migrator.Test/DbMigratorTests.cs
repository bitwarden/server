using Bit.Migrator;

namespace Migrator.Test;

public class DbMigratorTests
{
    private const int DefaultMinutes = 5;

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    public void ResolveExecutionTimeout_WithoutUsableOverride_UsesDefault(int? executionTimeoutSeconds)
    {
        var timeout = DbMigrator.ResolveExecutionTimeout(executionTimeoutSeconds, DefaultMinutes);

        Assert.Equal(TimeSpan.FromMinutes(DefaultMinutes), timeout);
    }

    [Fact]
    public void ResolveExecutionTimeout_ZeroOverride_MapsToNoLimit()
    {
        var timeout = DbMigrator.ResolveExecutionTimeout(0, DefaultMinutes);

        Assert.Equal(TimeSpan.Zero, timeout);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3600)]
    public void ResolveExecutionTimeout_WithOverride_UsesOverrideInSeconds(int executionTimeoutSeconds)
    {
        var timeout = DbMigrator.ResolveExecutionTimeout(executionTimeoutSeconds, DefaultMinutes);

        Assert.Equal(TimeSpan.FromSeconds(executionTimeoutSeconds), timeout);
    }
}
