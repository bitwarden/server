using Bit.Core.Entities;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Factories;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

internal sealed class CreateCollectionsStep : IStep
{
    private readonly int _count;
    private readonly OrgStructureModel? _structure;

    private CreateCollectionsStep(int count, OrgStructureModel? structure)
    {
        _count = count;
        _structure = structure;
    }

    internal static CreateCollectionsStep FromCount(int count) => new(count, null);

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
        var collectionUsers = new List<CollectionUser>();

        // User assignment: cycling 1-3 collections per user
        if (collections.Count > 0 && hardenedOrgUserIds.Count > 0)
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

        context.Collections.AddRange(collections);
        context.Registry.CollectionIds.AddRange(collectionIds);
        context.CollectionUsers.AddRange(collectionUsers);
    }
}
