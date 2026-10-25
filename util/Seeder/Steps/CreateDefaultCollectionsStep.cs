using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.RustSDK;
using Bit.Seeder.Data;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Generators;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Seeds the end state of the Organization Data Ownership migration: one default user collection
/// (<see cref="CollectionType.DefaultUserCollection"/>) per Confirmed User or Custom member, plus the personal
/// items each member has already moved into it.
/// </summary>
/// <remarks>
/// Collections are dated at <c>policyEnabledDate</c>. Migrated items keep a personal <c>CreationDate</c> before it
/// and take a <c>RevisionDate</c> between it and now, as the server's share operation leaves them. The collections
/// are not added to <see cref="EntityRegistry.CollectionIds"/>, so later steps never assign pool ciphers to them.
/// </remarks>
internal sealed class CreateDefaultCollectionsStep(
    SeedPresetDefaultCollections options,
    DateTime policyEnabledDate,
    int seed,
    Distribution<CipherType>? typeDist = null) : IStep
{
    private const string _defaultName = "My Items";

    public void Execute(SeederContext context)
    {
        var orgId = context.RequireOrgId();
        var orgKey = context.RequireOrgKey();
        var generator = context.RequireGenerator();
        var progress = context.GetProgress();

        // The server's policy exempts Owners and Admins; Confirmed is implied by UserDigests.
        var exempt = context.OrganizationUsers
            .Where(ou => ou.Type is OrganizationUserType.Owner or OrganizationUserType.Admin)
            .Select(ou => ou.Id)
            .ToHashSet();
        var members = context.Registry.UserDigests.Where(d => !exempt.Contains(d.OrgUserId)).ToList();

        var random = new Random(ItemSeedMixer.Mix(seed, members.Count));
        var activeCounts = ComputeActiveCounts(members.Count, random);
        var trashCounts = ComputeTrashCounts(activeCounts, random);
        var itemCounts = activeCounts.Zip(trashCounts, (active, trash) => active + trash).ToArray();
        var total = itemCounts.Sum();

        var encryptedName = RustSdkService.EncryptString(options.Name ?? _defaultName, orgKey);
        var collections = new Collection[members.Count];
        var collectionUsers = new CollectionUser[members.Count];

        for (var u = 0; u < members.Count; u++)
        {
            collections[u] = new Collection
            {
                Id = CombGuid.Generate(),
                OrganizationId = orgId,
                Name = encryptedName,
                Type = CollectionType.DefaultUserCollection,
                CreationDate = policyEnabledDate,
                RevisionDate = policyEnabledDate,
            };
            collectionUsers[u] = CollectionUserSeeder.Create(collections[u].Id, members[u].OrgUserId, manage: true);
        }

        context.Collections.AddRange(collections);
        context.CollectionUsers.AddRange(collectionUsers);

        progress?.Report(new PhaseStarted(SeederPhases.CreatingDefaultCollections, total));

        if (total > 0)
        {
            AddMigratedItems(context, collections, activeCounts, itemCounts, total, orgId, orgKey, generator, progress);
        }

        progress?.Report(new PhaseCompleted(SeederPhases.CreatingDefaultCollections));
    }

    private void AddMigratedItems(
        SeederContext context,
        Collection[] collections,
        int[] activeCounts,
        int[] itemCounts,
        int total,
        Guid orgId,
        string orgKey,
        GeneratorContext generator,
        IProgress<SeederProgressEvent>? progress)
    {
        var typeDistribution = typeDist ?? CipherTypeDistributions.Realistic;
        var passwordDistribution = PasswordDistributions.Realistic;
        var companies = Companies.All;
        var loginCount = CipherComposer.CountLogins(typeDistribution, total);

        // Force lazy generator init before parallel loop (prevents ??= data race)
        _ = (generator.Username, generator.Card, generator.Identity, generator.SecureNote, generator.UniquePassword, generator.LoginNotes, generator.CustomField, generator.TotpSecret);

        var offsets = new int[itemCounts.Length];
        for (int u = 0, running = 0; u < itemCounts.Length; u++)
        {
            offsets[u] = running;
            running += itemCounts[u];
        }

        // Generators seed per index, so continuing past the org vault's indices keeps plaintext passwords distinct within the org.
        var indexBase = context.Ciphers.Count;
        var now = DateTime.UtcNow;
        var migrationWindow = Math.Max((now - policyEnabledDate).TotalSeconds, 1);
        var perMember = new (Cipher[] Ciphers, CollectionCipher[] Links)[itemCounts.Length];
        var batchSize = Math.Max(1, total / 100);

        Parallel.For(
            0,
            itemCounts.Length,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            localInit: () => 0,
            body: (u, _, localTicked) =>
            {
                var localRandom = new Random(ItemSeedMixer.Mix(seed, u));
                var count = itemCounts[u];
                var ciphers = new Cipher[count];
                var links = new CollectionCipher[count];
                var migratedAt = policyEnabledDate.AddSeconds(localRandom.NextDouble() * migrationWindow);

                for (var i = 0; i < count; i++)
                {
                    var localIndex = offsets[u] + i;
                    var cipherType = typeDistribution.Select(localIndex, total);
                    var cipher = CipherComposer.Compose(indexBase + localIndex, loginCount, cipherType, orgKey, companies, generator,
                        passwordDistribution, organizationId: orgId);
                    cipher.CreationDate = policyEnabledDate.AddDays(-localRandom.Next(1, 1501));
                    cipher.RevisionDate = migratedAt;

                    if (i >= activeCounts[u])
                    {
                        cipher.DeletedDate = migratedAt.AddSeconds(localRandom.NextDouble() * (now - migratedAt).TotalSeconds);
                        cipher.RevisionDate = cipher.DeletedDate.Value;
                    }

                    ciphers[i] = cipher;
                    links[i] = new CollectionCipher { CipherId = cipher.Id, CollectionId = collections[u].Id };
                }

                perMember[u] = (ciphers, links);

                localTicked += count;
                if (progress is not null && localTicked >= batchSize)
                {
                    progress.Report(new PhaseAdvanced(SeederPhases.CreatingDefaultCollections, localTicked));
                    localTicked = 0;
                }
                return localTicked;
            },
            localFinally: localTicked =>
            {
                if (progress is not null && localTicked > 0)
                {
                    progress.Report(new PhaseAdvanced(SeederPhases.CreatingDefaultCollections, localTicked));
                }
            });

        foreach (var (ciphers, links) in perMember)
        {
            context.Ciphers.AddRange(ciphers);
            context.Registry.CipherIds.AddRange(ciphers.Select(c => c.Id));
            context.CollectionCiphers.AddRange(links);
        }
    }

    /// <summary>
    /// Active item counts per member: <see cref="SeedPresetDefaultCollections.NonEmptyRate"/> of members are
    /// non-empty, the first of them get <see cref="SeedPresetDefaultCollections.Largest"/>, the rest draw from
    /// <see cref="SeedPresetDefaultCollections.Sizes"/> (1 item each without it). Members are chosen by shuffle.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <c>largest</c> has more entries than non-empty members.</exception>
    private int[] ComputeActiveCounts(int memberCount, Random random)
    {
        var counts = new int[memberCount];
        var nonEmpty = (int)Math.Round(memberCount * (options.NonEmptyRate ?? 0));
        var largest = options.Largest ?? [];

        if (largest.Count > nonEmpty)
        {
            throw new InvalidOperationException(
                $"'defaultCollections.largest' has {largest.Count} entries but only {nonEmpty} members are non-empty " +
                $"(nonEmptyRate × {memberCount} eligible members). Raise nonEmptyRate or shorten largest.");
        }

        if (nonEmpty == 0)
        {
            return counts;
        }

        var drawCount = nonEmpty - largest.Count;
        var drawn = options.Sizes is { Count: > 0 }
            ? new SizeHistogram(options.Sizes.Select(b => (b.Min, b.Max, b.Weight))).Draw(drawCount, random)
            : Enumerable.Repeat(1, drawCount).ToArray();

        var chosen = Enumerable.Range(0, memberCount).ToArray();
        random.Shuffle(chosen);

        var values = largest.Concat(drawn).ToArray();
        for (var k = 0; k < values.Length; k++)
        {
            counts[chosen[k]] = values[k];
        }

        return counts;
    }

    /// <summary>
    /// Trashed items per member on top of the active ones (<see cref="SeedPresetDefaultCollections.DeletedRate"/>
    /// of the active count, randomly rounded), so the active counts keep their shape.
    /// </summary>
    private int[] ComputeTrashCounts(int[] activeCounts, Random random)
    {
        var deletedRate = options.DeletedRate ?? 0;
        var trashCounts = new int[activeCounts.Length];

        for (var u = 0; u < activeCounts.Length; u++)
        {
            if (activeCounts[u] > 0 && deletedRate > 0)
            {
                trashCounts[u] = (int)(activeCounts[u] * deletedRate + random.NextDouble());
            }
        }

        return trashCounts;
    }
}
