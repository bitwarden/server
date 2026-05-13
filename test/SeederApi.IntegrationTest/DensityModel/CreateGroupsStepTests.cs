using Bit.Seeder.Data.Enums;
using Bit.Seeder.Options;
using Bit.Seeder.Steps;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.DensityModel;

public class CreateGroupsStepTests
{
    [Fact]
    public void ComputeUsersPerGroup_MegaGroup_GroupZeroDoesNotParticipateInRemainder()
    {
        var step = CreateStep(MembershipDistributionShape.MegaGroup, skew: 0.5);

        var allocations = step.ComputeUsersPerGroup(5, 100);

        var megaFraction = 0.5 + 0.5 * 0.45; // 0.725
        var expectedMega = (int)(100 * megaFraction); // 72
        Assert.Equal(expectedMega, allocations[0]);
    }

    [Fact]
    public void ComputeUsersPerGroup_MegaGroup_RemainderGoesToNonMegaGroups()
    {
        var step = CreateStep(MembershipDistributionShape.MegaGroup, skew: 0.5);

        var allocations = step.ComputeUsersPerGroup(5, 100);

        var nonMegaTotal = allocations[1] + allocations[2] + allocations[3] + allocations[4];
        Assert.Equal(100 - allocations[0], nonMegaTotal);
        Assert.True(allocations[1] > 0, "Non-mega groups should have members");
    }

    [Fact]
    public void ComputeUsersPerGroup_MegaGroup_SingleGroup_AllUsersAssigned()
    {
        var step = CreateStep(MembershipDistributionShape.MegaGroup, skew: 0.9);

        var allocations = step.ComputeUsersPerGroup(1, 100);

        Assert.Single(allocations);
        Assert.Equal(100, allocations[0]);
    }

    [Fact]
    public void ComputeUsersPerGroup_MegaGroup_SingleUser_SingleGroup()
    {
        var step = CreateStep(MembershipDistributionShape.MegaGroup, skew: 1.0);

        var allocations = step.ComputeUsersPerGroup(1, 1);

        Assert.Equal(1, allocations[0]);
    }

    [Fact]
    public void ComputeUsersPerGroup_MegaGroup_SumsToUserCount()
    {
        var step = CreateStep(MembershipDistributionShape.MegaGroup, skew: 0.8);

        var allocations = step.ComputeUsersPerGroup(10, 100);

        Assert.Equal(100, allocations.Sum());
    }

    [Fact]
    public void ComputeUsersPerGroup_PowerLaw_FirstGroupIsLargest()
    {
        var step = CreateStep(MembershipDistributionShape.PowerLaw, skew: 0.8);

        var allocations = step.ComputeUsersPerGroup(10, 100);

        Assert.Equal(allocations.Max(), allocations[0]);
    }

    [Fact]
    public void ComputeUsersPerGroup_PowerLaw_HighSkewMoreConcentrated()
    {
        var gentle = CreateStep(MembershipDistributionShape.PowerLaw, skew: 0.0);
        var steep = CreateStep(MembershipDistributionShape.PowerLaw, skew: 1.0);

        var gentleAllocations = gentle.ComputeUsersPerGroup(10, 100);
        var steepAllocations = steep.ComputeUsersPerGroup(10, 100);

        Assert.True(steepAllocations[0] > gentleAllocations[0],
            $"Steep skew group 0 ({steepAllocations[0]}) should be larger than gentle ({gentleAllocations[0]})");
    }

    [Fact]
    public void ComputeUsersPerGroup_PowerLaw_MoreGroupsThanUsers_NoNegativeAllocations()
    {
        var step = CreateStep(MembershipDistributionShape.PowerLaw, skew: 1.0);

        var allocations = step.ComputeUsersPerGroup(20, 5);

        Assert.All(allocations, a => Assert.True(a >= 0, $"Allocation should be >= 0, got {a}"));
        Assert.Equal(5, allocations.Sum());
    }

    [Fact]
    public void ComputeUsersPerGroup_PowerLaw_SumsToUserCount()
    {
        var step = CreateStep(MembershipDistributionShape.PowerLaw, skew: 0.5);

        var allocations = step.ComputeUsersPerGroup(10, 100);

        Assert.Equal(100, allocations.Sum());
    }

    [Fact]
    public void ComputeUsersPerGroup_Uniform_EvenDistribution()
    {
        var step = CreateStep(MembershipDistributionShape.Uniform);

        var allocations = step.ComputeUsersPerGroup(5, 100);

        Assert.All(allocations, a => Assert.Equal(20, a));
    }

    [Fact]
    public void ComputeUsersPerGroup_Uniform_SumsToUserCount()
    {
        var step = CreateStep(MembershipDistributionShape.Uniform);

        var allocations = step.ComputeUsersPerGroup(7, 100);

        Assert.Equal(100, allocations.Sum());
    }

    private static CreateGroupsStep CreateStep(MembershipDistributionShape shape, double skew = 0.0)
    {
        var density = new DensityProfile
        {
            MembershipShape = shape,
            MembershipSkew = skew
        };
        return new CreateGroupsStep(0, density);
    }
}
