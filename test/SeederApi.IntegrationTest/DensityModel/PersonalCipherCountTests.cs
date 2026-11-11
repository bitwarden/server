using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Steps;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.DensityModel;

public class PersonalCipherCountTests
{
    [Fact]
    public void ComputeUserCounts_PendingMigration_MatchesProductionShape()
    {
        // 3,941 = the confirmed members plus the owner of a large org before the Organization Data Ownership migration
        const int users = 3941;

        var counts = GeneratePersonalCiphersStep.ComputeUserCounts(PersonalCipherDistributions.PendingMigration, 0, users);

        // Prod: 718 of 3,644 confirmed members (19.7%) hold 22.6k personal items; 3 hold more than 1,000
        var holders = counts.Count(c => c > 0);
        Assert.InRange(holders, 709, 828);
        Assert.InRange(counts.Sum(), 20_000, 27_000);
        Assert.Equal(3, counts.Count(c => c > 1000));

        // The computation is deterministic, so the exact shape is pinned too
        Assert.Equal(778, holders);
        Assert.Equal(24_875, counts.Sum());
        Assert.Equal(new[] { 1941, 1940, 1939 }, counts.OrderDescending().Take(3));
    }

    [Fact]
    public void ComputeUserCounts_NoDistribution_UsesFlatCount()
    {
        var counts = GeneratePersonalCiphersStep.ComputeUserCounts(null, 7, 5);

        Assert.Equal(new[] { 7, 7, 7, 7, 7 }, counts);
    }
}
