using Xunit;

namespace Bit.Core.Test.Billing.Mocks.Plans;

public class TeamsPlanMockTests
{
    // Locks in the TeamsPlan mock baselines the Teams 2020 + SM migration tests depend on: Teams Annually
    // keeps the 2020 SM baseline of 50 (50 -> 50 => grace 0), whereas Teams Monthly drops to 20 (50 -> 20 => grace 30).
    [Fact]
    public void TeamsAnnually_KeepsTeams2020SecretsManagerBaseline()
    {
        Assert.Equal(
            new Teams2020Plan(true).SecretsManager!.BaseServiceAccount,
            new TeamsPlan(true).SecretsManager!.BaseServiceAccount);
    }

    [Fact]
    public void TeamsMonthly_DropsSecretsManagerBaselineFromTeams2020()
    {
        Assert.Equal(50, new Teams2020Plan(false).SecretsManager!.BaseServiceAccount);
        Assert.Equal(20, new TeamsPlan(false).SecretsManager!.BaseServiceAccount);
    }
}
