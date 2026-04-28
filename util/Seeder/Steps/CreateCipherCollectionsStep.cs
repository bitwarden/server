using Bit.Core.Entities;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Resolves preset collection assignments, creating <see cref="CollectionCipher"/> entities for each
/// <c>(cipher, collection)</c> tuple declared in the preset.
/// </summary>
internal sealed class CreateCipherCollectionsStep(List<SeedCollectionAssignment> assignments) : IStep
{
    public void Execute(SeederContext context)
    {
        var cipherNames = context.Registry.FixtureCipherNameToId;
        var collectionNames = context.Registry.FixtureCollectionNameToId;

        // Phase 1: Validate all references before any mutations
        foreach (var a in assignments)
        {
            if (!cipherNames.ContainsKey(a.Cipher))
            {
                var available = string.Join(", ", cipherNames.Keys.OrderBy(k => k));
                throw new InvalidOperationException(
                    $"Collection assignment references unknown cipher '{a.Cipher}'. Available ciphers: {available}");
            }

            if (!collectionNames.ContainsKey(a.Collection))
            {
                var available = string.Join(", ", collectionNames.Keys.OrderBy(k => k));
                throw new InvalidOperationException(
                    $"Collection assignment references unknown collection '{a.Collection}'. Available collections: {available}");
            }
        }

        // Phase 2: Accumulate (cipherId → [collectionIds]) and detect duplicates
        var cipherCollectionMap = new Dictionary<Guid, List<Guid>>();
        var seen = new HashSet<(Guid CipherId, Guid CollectionId)>();

        foreach (var a in assignments)
        {
            var cipherId = cipherNames[a.Cipher];
            var collectionId = collectionNames[a.Collection];

            if (!seen.Add((cipherId, collectionId)))
            {
                throw new InvalidOperationException(
                    $"Duplicate collection assignment: cipher '{a.Cipher}' + collection '{a.Collection}' appears more than once.");
            }

            if (!cipherCollectionMap.TryGetValue(cipherId, out var collectionIds))
            {
                collectionIds = [];
                cipherCollectionMap[cipherId] = collectionIds;
            }

            collectionIds.Add(collectionId);
        }

        // Phase 3: Create CollectionCipher entities
        foreach (var (cipherId, collectionIds) in cipherCollectionMap)
        {
            foreach (var collectionId in collectionIds)
            {
                context.CollectionCiphers.Add(new CollectionCipher
                {
                    CipherId = cipherId,
                    CollectionId = collectionId
                });
            }
        }
    }
}
