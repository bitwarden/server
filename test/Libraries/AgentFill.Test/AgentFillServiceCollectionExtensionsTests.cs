using Bit.Core.Settings;
using Xunit;

namespace Bit.AgentFill.Test;

public class AgentFillServiceCollectionExtensionsTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("sqlServer", true)]
    [InlineData("postgres", false)]
    [InlineData("PostgreSQL", false)]
    [InlineData("mysql", false)]
    [InlineData("mariadb", false)]
    [InlineData("sqlite", false)]
    public void UsesDapper_MatchesTheHostsProviderSelection(string? provider, bool expected)
    {
        Assert.Equal(expected, AgentFillServiceCollectionExtensions.UsesDapper(new GlobalSettings { DatabaseProvider = provider }));
    }
}
