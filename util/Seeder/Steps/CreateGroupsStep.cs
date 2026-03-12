using Bit.Core.AdminConsole.Entities;
using Bit.Seeder.Factories;
using Bit.Seeder.Options;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

internal sealed class CreateGroupsStep(int count, DensityProfile? density = null) : IStep
{
    private readonly DensityProfile? _density = density;

    public void Execute(SeederContext context)
    {
        var orgId = context.RequireOrgId();
        var hardenedOrgUserIds = context.Registry.HardenedOrgUserIds;

        var groups = new List<Group>(count);
        var groupIds = new List<Guid>(count);
        var groupUsers = new List<GroupUser>(hardenedOrgUserIds.Count);

        for (var i = 0; i < count; i++)
        {
            var group = GroupSeeder.Create(orgId, $"Group {i + 1}");
            groups.Add(group);
            groupIds.Add(group.Id);
        }

        context.Groups.AddRange(groups);

        if (_density == null)
        {
            if (groups.Count > 0 && hardenedOrgUserIds.Count > 0)
            {
                for (var i = 0; i < hardenedOrgUserIds.Count; i++)
                {
                    var groupId = groupIds[i % groups.Count];
                    groupUsers.Add(GroupUserSeeder.Create(groupId, hardenedOrgUserIds[i]));
                }
            }

            context.Registry.GroupIds.AddRange(groupIds);
        }
        else
        {
            var emptyCount = (int)(groups.Count * _density.EmptyGroupRate);
            var activeGroupIds = groupIds.Take(groups.Count - emptyCount).ToList();

            context.Registry.GroupIds.AddRange(activeGroupIds);

            if (activeGroupIds.Count > 0 && hardenedOrgUserIds.Count > 0)
            {
                var allocations = ComputeUsersPerGroup(activeGroupIds.Count, hardenedOrgUserIds.Count);
                var userIndex = 0;
                for (var g = 0; g < activeGroupIds.Count; g++)
                {
                    for (var u = 0; u < allocations[g]; u++)
                    {
                        groupUsers.Add(GroupUserSeeder.Create(activeGroupIds[g], hardenedOrgUserIds[userIndex++]));
                    }
                }
            }
        }

        context.GroupUsers.AddRange(groupUsers);
    }

    internal int[] ComputeUsersPerGroup(int groupCount, int userCount)
    {
        var allocations = new int[groupCount];

        switch (_density!.MembershipShape)
        {
            case Data.Enums.MembershipDistributionShape.Uniform:
                for (var i = 0; i < userCount; i++)
                {
                    allocations[i % groupCount]++;
                }
                break;

            case Data.Enums.MembershipDistributionShape.PowerLaw:
                // Maps MembershipSkew [0,1] to Zipf exponent [0.5, 2.0]
                var exponent = 0.5 + _density.MembershipSkew * 1.5;
                var fractional = new double[groupCount];
                var totalWeight = 0.0;
                for (var i = 0; i < groupCount; i++)
                {
                    fractional[i] = 1.0 / Math.Pow(i + 1, exponent);
                    totalWeight += fractional[i];
                }

                var assigned = 0;
                for (var i = 0; i < groupCount; i++)
                {
                    fractional[i] = fractional[i] / totalWeight * userCount;
                    allocations[i] = (int)fractional[i];
                    assigned += allocations[i];
                }

                // Largest-remainder: give +1 to groups that lost the most from truncation
                var remainder = userCount - assigned;
                if (remainder > 0)
                {
                    var indices = Enumerable.Range(0, groupCount)
                        .OrderByDescending(i => fractional[i] - allocations[i])
                        .Take(remainder);
                    foreach (var i in indices)
                    {
                        allocations[i]++;
                    }
                }
                break;

            case Data.Enums.MembershipDistributionShape.MegaGroup:
                // Maps MembershipSkew [0,1] to mega group share [50%, 95%]
                var megaFraction = 0.5 + _density.MembershipSkew * 0.45;
                var megaCount = (int)(userCount * megaFraction);
                allocations[0] = megaCount;
                var remaining = userCount - megaCount;
                if (groupCount > 1)
                {
                    for (var i = 0; i < remaining; i++)
                    {
                        allocations[1 + (i % (groupCount - 1))]++;
                    }
                }
                else
                {
                    allocations[0] += remaining;
                }
                break;

            default:
                throw new InvalidOperationException(
                    $"Unhandled MembershipDistributionShape: {_density.MembershipShape}");
        }

        return allocations;
    }
}
