using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.RustSDK;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Seeds the end state of the Organization Data Ownership migration: one My Items
/// (<see cref="CollectionType.DefaultUserCollection"/>) collection per confirmed member, plus the
/// personal items each member has already moved into it.
/// </summary>
/// <remarks>
/// <para>Mirrors what the server and clients produce:</para>
/// <list type="bullet">
/// <item><description>Collections match <c>Collection_CreateDefaultCollections</c>: Type 1, no ExternalId or
/// DefaultUserCollectionEmail, created when the policy was enabled, one <c>CollectionUser</c> grant to the owner
/// with Manage and no group grants. Every collection shares a single org-key ciphertext of the name, because the
/// client encrypts the name once in the policy request.</description></item>
/// <item><description>Migrated items are org ciphers encrypted with the org key (what <c>PUT /ciphers/share</c> leaves
/// behind), created before the policy date, revised after it, in exactly one collection — the owner's My Items.</description></item>
/// </list>
/// </remarks>
internal sealed class CreateMyItemsStep(
    SeedPresetMyItems shape,
    DateTime policyEnabledDate,
    int seed,
    Distribution<CipherType>? typeDist = null) : IStep
{
    public void Execute(SeederContext context)
    {
        var orgId = context.RequireOrgId();
        var orgKey = context.RequireOrgKey();
        var generator = context.RequireGenerator();
        var members = context.Registry.UserDigests;
        var progress = context.GetProgress();
        var random = new Random(seed + 2);

        var encryptedName = RustSdkService.EncryptString(shape.Name ?? "My Items", orgKey);
        var collections = new List<Collection>(members.Count);
        var collectionUsers = new List<CollectionUser>(members.Count);

        foreach (var member in members)
        {
            var collection = new Collection
            {
                Id = CombGuid.Generate(),
                OrganizationId = orgId,
                Name = encryptedName,
                Type = CollectionType.DefaultUserCollection,
                CreationDate = policyEnabledDate,
                RevisionDate = policyEnabledDate,
            };
            collections.Add(collection);
            collectionUsers.Add(CollectionUserSeeder.Create(collection.Id, member.OrgUserId, manage: true));
        }

        var itemCounts = ComputeItemCounts(members.Count, random);
        var total = itemCounts.Sum();

        progress?.Report(new PhaseStarted(SeederPhases.CreatingMyItems, total));

        var typeDistribution = typeDist ?? CipherTypeDistributions.Realistic;
        var passwordDistribution = PasswordDistributions.Realistic;
        var companies = Companies.All;
        _ = (generator.Username, generator.Card, generator.Identity, generator.SecureNote);

        var offsets = new int[members.Count];
        for (int u = 0, running = 0; u < members.Count; u++)
        {
            offsets[u] = running;
            running += itemCounts[u];
        }

        // Keep generated plaintext distinct from the org-cipher pool, which uses indices from 0
        const int indexBase = 10_000_000;
        var now = DateTime.UtcNow;
        var migrationWindow = Math.Max((now - policyEnabledDate).TotalSeconds, 1);
        var perMember = new (Cipher[] Ciphers, CollectionCipher[] Links)[members.Count];

        Parallel.For(0, members.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, u =>
        {
            var localRandom = new Random(seed ^ (u * 7919));
            var n = itemCounts[u];
            var ciphers = new Cipher[n];
            var links = new CollectionCipher[n];
            var migratedAt = policyEnabledDate.AddSeconds(localRandom.NextDouble() * migrationWindow);

            for (var i = 0; i < n; i++)
            {
                var globalIndex = offsets[u] + i;
                var cipherType = typeDistribution.Select(globalIndex, Math.Max(total, 1));
                var cipher = CipherComposer.Compose(indexBase + globalIndex, cipherType, orgKey, companies, generator,
                    passwordDistribution, organizationId: orgId);
                cipher.CreationDate = policyEnabledDate.AddDays(-localRandom.Next(1, 1500));
                cipher.RevisionDate = migratedAt;
                ciphers[i] = cipher;
                links[i] = new CollectionCipher { CipherId = cipher.Id, CollectionId = collections[u].Id };
            }

            perMember[u] = (ciphers, links);
            if (n > 0)
            {
                progress?.Report(new PhaseAdvanced(SeederPhases.CreatingMyItems, n));
            }
        });

        context.Collections.AddRange(collections);
        context.CollectionUsers.AddRange(collectionUsers);
        foreach (var (ciphers, links) in perMember)
        {
            context.Ciphers.AddRange(ciphers);
            context.Registry.CipherIds.AddRange(ciphers.Select(c => c.Id));
            context.CollectionCiphers.AddRange(links);
        }

        progress?.Report(new PhaseCompleted(SeederPhases.CreatingMyItems));
    }

    /// <summary>
    /// Per-member item counts: <see cref="SeedPresetMyItems.NonEmptyRate"/> of members are non-empty, the
    /// heaviest get <see cref="SeedPresetMyItems.Largest"/>, the rest draw from the histogram. The owner
    /// (index 0) is skipped for the pinned tail so manual logins stay fast.
    /// </summary>
    private int[] ComputeItemCounts(int memberCount, Random random)
    {
        var counts = new int[memberCount];
        var nonEmpty = (int)Math.Round(memberCount * (shape.NonEmptyRate ?? 0));
        var largest = shape.Largest ?? [];
        if (nonEmpty == 0)
        {
            return counts;
        }

        var drawCount = Math.Max(nonEmpty - largest.Count, 0);
        var drawn = shape.Sizes is { Count: > 0 }
            ? new SizeHistogram(shape.Sizes.Select(b => (b.Min, b.Max, b.Weight))).Draw(drawCount, random)
            : Enumerable.Repeat(1, drawCount).ToArray();

        var chosen = Enumerable.Range(1, memberCount - 1).ToArray();
        random.Shuffle(chosen);
        var values = largest.Concat(drawn).ToArray();
        for (var k = 0; k < values.Length && k < chosen.Length; k++)
        {
            counts[chosen[k]] = values[k];
        }

        return counts;
    }
}
