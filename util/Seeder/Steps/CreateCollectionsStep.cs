using Bit.Core.Entities;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Factories;
using Bit.Seeder.Options;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

internal sealed class CreateCollectionsStep : IStep
{
    private readonly int _count;
    private readonly OrgStructureModel? _structure;
    private readonly DensityProfile? _density;

    private CreateCollectionsStep(int count, OrgStructureModel? structure, DensityProfile? density = null)
    {
        _count = count;
        _structure = structure;
        _density = density;
    }

    internal static CreateCollectionsStep FromCount(int count, DensityProfile? density = null) => new(count, null, density);

    internal static CreateCollectionsStep FromStructure(OrgStructureModel structure) => new(0, structure);

    public void Execute(SeederContext context)
    {
        var orgId = context.RequireOrgId();
        var orgKey = context.RequireOrgKey();
        var hardenedOrgUserIds = context.Registry.HardenedOrgUserIds;

        List<Collection> collections;

        if (_structure.HasValue)
        {
            var orgStructure = OrgStructures.GetStructure(_structure.Value);
            collections = orgStructure.Units
                .Select(unit => CollectionSeeder.Create(orgId, orgKey, unit.Name))
                .ToList();
        }
        else
        {
            collections = Enumerable.Range(0, _count)
                .Select(i => CollectionSeeder.Create(orgId, orgKey, $"Collection {i + 1}"))
                .ToList();
        }

        var collectionIds = collections.Select(c => c.Id).ToList();

        context.Collections.AddRange(collections);
        context.Registry.CollectionIds.AddRange(collectionIds);

        if (collections.Count == 0)
        {
            return;
        }

        if (_density == null)
        {
            var collectionUsers = new List<CollectionUser>();
            if (hardenedOrgUserIds.Count > 0)
            {
                foreach (var (orgUserId, userIndex) in hardenedOrgUserIds.Select((id, i) => (id, i)))
                {
                    var maxAssignments = Math.Min((userIndex % 3) + 1, collections.Count);
                    for (var j = 0; j < maxAssignments; j++)
                    {
                        collectionUsers.Add(CollectionUserSeeder.Create(
                            collections[(userIndex + j) % collections.Count].Id,
                            orgUserId,
                            readOnly: j > 0,
                            manage: j == 0));
                    }
                }
            }
            context.CollectionUsers.AddRange(collectionUsers);
            return;
        }

        var groupIds = context.Registry.GroupIds;

        if (_density.DirectAccessRatio < 1.0 && groupIds.Count > 0)
        {
            var collectionGroups = BuildCollectionGroups(collectionIds, groupIds);
            ApplyGroupPermissions(collectionGroups, _density.PermissionDistribution);
            context.CollectionGroups.AddRange(collectionGroups);
        }

        var directUserCount = (int)(hardenedOrgUserIds.Count * _density.DirectAccessRatio);
        if (directUserCount > 0)
        {
            var directUsers = BuildCollectionUsers(collectionIds, hardenedOrgUserIds, directUserCount);
            ApplyUserPermissions(directUsers, _density.PermissionDistribution);
            context.CollectionUsers.AddRange(directUsers);
        }
    }

    internal List<CollectionGroup> BuildCollectionGroups(List<Guid> collectionIds, List<Guid> groupIds)
    {
        var min = _density!.CollectionFanOutMin;
        var max = _density.CollectionFanOutMax;
        var result = new List<CollectionGroup>(collectionIds.Count * (min + max + 1) / 2);

        for (var c = 0; c < collectionIds.Count; c++)
        {
            var fanOut = ComputeFanOut(c, collectionIds.Count, min, max);
            fanOut = Math.Min(fanOut, groupIds.Count);

            for (var g = 0; g < fanOut; g++)
            {
                result.Add(CollectionGroupSeeder.Create(
                    collectionIds[c],
                    groupIds[(c + g) % groupIds.Count]));
            }
        }

        return result;
    }

    internal int ComputeFanOut(int collectionIndex, int collectionCount, int min, int max)
    {
        var range = max - min + 1;
        if (range <= 1)
        {
            return min;
        }

        switch (_density!.FanOutShape)
        {
            case CollectionFanOutShape.PowerLaw:
                // Zipf weight normalized against index 0 (where weight = 1.0), scaled to [min, max]
                var weight = 1.0 / Math.Pow(collectionIndex + 1, 0.8);
                return min + (int)(weight * (range - 1) + 0.5);

            case CollectionFanOutShape.FrontLoaded:
                // First 10% of collections get max fan-out, rest get min
                var topCount = Math.Max(1, collectionCount / 10);
                return collectionIndex < topCount ? max : min;

            case CollectionFanOutShape.Uniform:
                return min + (collectionIndex % range);

            default:
                throw new InvalidOperationException(
                    $"Unhandled CollectionFanOutShape: {_density.FanOutShape}");
        }
    }

    internal static List<CollectionUser> BuildCollectionUsers(
        List<Guid> collectionIds, List<Guid> userIds, int directUserCount)
    {
        var result = new List<CollectionUser>(directUserCount * 2);
        for (var i = 0; i < directUserCount; i++)
        {
            var maxAssignments = Math.Min((i % 3) + 1, collectionIds.Count);
            for (var j = 0; j < maxAssignments; j++)
            {
                result.Add(CollectionUserSeeder.Create(
                    collectionIds[(i + j) % collectionIds.Count],
                    userIds[i]));
            }
        }
        return result;
    }

    private static (bool ReadOnly, bool HidePasswords, bool Manage) ResolvePermission(
        Distribution<PermissionWeight> distribution, int index, int total)
    {
        var weight = distribution.Select(index, total);
        return weight switch
        {
            PermissionWeight.ReadOnly => (true, false, false),
            PermissionWeight.HidePasswords => (false, true, false),
            PermissionWeight.Manage => (false, false, true),
            PermissionWeight.ReadWrite => (false, false, false),
            _ => throw new InvalidOperationException(
                $"Unhandled PermissionWeight: {weight}")
        };
    }

    internal static void ApplyGroupPermissions(
        List<CollectionGroup> assignments, Distribution<PermissionWeight> distribution)
    {
        for (var i = 0; i < assignments.Count; i++)
        {
            var (readOnly, hidePasswords, manage) = ResolvePermission(distribution, i, assignments.Count);
            assignments[i].ReadOnly = readOnly;
            assignments[i].HidePasswords = hidePasswords;
            assignments[i].Manage = manage;
        }
    }

    internal static void ApplyUserPermissions(
        List<CollectionUser> assignments, Distribution<PermissionWeight> distribution)
    {
        for (var i = 0; i < assignments.Count; i++)
        {
            var (readOnly, hidePasswords, manage) = ResolvePermission(distribution, i, assignments.Count);
            assignments[i].ReadOnly = readOnly;
            assignments[i].HidePasswords = hidePasswords;
            assignments[i].Manage = manage;
        }
    }
}
