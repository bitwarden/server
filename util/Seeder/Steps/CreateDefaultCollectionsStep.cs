using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.RustSDK;
using Bit.Seeder.Data;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
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
/// Members who still hold personal items pending migration keep an empty collection, as on the server: the client
/// moves every personal item in one pass, and the policy blocks new personal items afterwards.
/// </remarks>
internal sealed class CreateDefaultCollectionsStep(
    SeedPresetDefaultCollections options,
    DateTime policyEnabledDate,
    int seed,
    Distribution<CipherType>? typeDist = null,
    Distribution<(int Min, int Max)>? personalDist = null,
    int personalCountPerUser = 0,
    DefaultCollectionSizing sizing = DefaultCollectionSizing.Sampled) : IStep
{
    private const string _defaultName = "My Items";

    public void Execute(SeederContext context)
    {
        var orgId = context.RequireOrgId();
        var orgKey = context.RequireOrgKey();
        var generator = context.RequireGenerator();
        var progress = context.Progress;

        // The server's policy exempts Owners and Admins; Confirmed is implied by UserDigests.
        var exempt = context.OrganizationUsers
            .Where(ou => ou.Type is OrganizationUserType.Owner or OrganizationUserType.Admin)
            .Select(ou => ou.Id)
            .ToHashSet();
        var members = context.Registry.UserDigests.Where(d => !exempt.Contains(d.OrgUserId)).ToList();
        var pending = PendingOrgUserIds(context.Registry.UserDigests);
        var candidates = Enumerable.Range(0, members.Count).Where(u => !pending.Contains(members[u].OrgUserId)).ToArray();

        var random = new Random(ItemSeedMixer.Mix(seed, members.Count));
        var activeCounts = ComputeActiveCounts(members.Count, candidates, random);
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
    /// Org user ids of the members that <see cref="GeneratePersonalCiphersStep"/> will give personal items, from the
    /// same counts it computes over the same digests.
    /// </summary>
    private HashSet<Guid> PendingOrgUserIds(List<EntityRegistry.UserDigest> userDigests)
    {
        if (personalDist is null && personalCountPerUser == 0)
        {
            return [];
        }

        var counts = GeneratePersonalCiphersStep.ComputeUserCounts(personalDist, personalCountPerUser, userDigests.Count);
        return userDigests.Where((_, u) => counts[u] > 0).Select(d => d.OrgUserId).ToHashSet();
    }

    /// <summary>
    /// Active item counts per member: <see cref="SeedPresetDefaultCollections.NonEmptyRate"/> of members are
    /// non-empty, the first of them get <see cref="SeedPresetDefaultCollections.Largest"/>, the rest draw from
    /// <see cref="SeedPresetDefaultCollections.Sizes"/> (1 item each without it), scaled to
    /// <see cref="SeedPresetDefaultCollections.TargetActiveItems"/> when it is set. Non-empty members are chosen by
    /// shuffling <paramref name="candidates"/>, the members with no pending personal items; the rest stay empty.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>largest</c> has more entries than non-empty members, <c>targetActiveItems</c> is out of reach,
    /// or there are more non-empty members than candidates.
    /// </exception>
    private int[] ComputeActiveCounts(int memberCount, int[] candidates, Random random)
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

        var drawCount = nonEmpty - largest.Count;
        var target = options.TargetActiveItems;
        var cap = options.Sizes is { Count: > 0 } sizes ? sizes.Max(b => b.Max) : 1;
        if (target is not null)
        {
            RequireReachableTarget(target.Value, largest.Sum(), drawCount, cap, memberCount);
        }

        if (nonEmpty == 0)
        {
            return counts;
        }

        var drawn = DrawSizes(drawCount, random);
        if (target is not null)
        {
            ScaleToTarget(drawn, target.Value, largest.Sum(), cap);
        }

        var values = largest.Concat(drawn).ToArray();
        if (values.Length > candidates.Length)
        {
            throw new InvalidOperationException(
                $"'defaultCollections.nonEmptyRate' needs {values.Length} non-empty collections but only " +
                $"{candidates.Length} members have no pending personal items. Lower nonEmptyRate or give fewer members personal items.");
        }

        random.Shuffle(candidates);
        for (var k = 0; k < values.Length; k++)
        {
            counts[candidates[k]] = values[k];
        }

        return counts;
    }

    /// <summary>
    /// Sizes for the non-empty members after <c>largest</c>: random draws from the histogram for
    /// <see cref="DefaultCollectionSizing.Sampled"/>, its mid-quantiles for <see cref="DefaultCollectionSizing.Quantile"/>,
    /// and 1 each without <see cref="SeedPresetDefaultCollections.Sizes"/>.
    /// </summary>
    private int[] DrawSizes(int count, Random random)
    {
        if (options.Sizes is not { Count: > 0 })
        {
            return Enumerable.Repeat(1, count).ToArray();
        }

        var histogram = new SizeHistogram(options.Sizes.Select(b => (b.Min, b.Max, b.Weight)));
        return sizing switch
        {
            DefaultCollectionSizing.Sampled => histogram.Draw(count, random),
            DefaultCollectionSizing.Quantile => histogram.Quantiles(count),
        };
    }

    /// <summary>
    /// Rejects a <c>targetActiveItems</c> that the drawn members cannot reach at 1 to <paramref name="cap"/> items
    /// each on top of the untouched <c>largest</c> entries.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the target is outside that range.</exception>
    private static void RequireReachableTarget(int target, int largestSum, int drawCount, int cap, int memberCount)
    {
        var lowest = (long)largestSum + drawCount;
        var highest = largestSum + (long)drawCount * cap;
        if (target < lowest || target > highest)
        {
            throw new InvalidOperationException(
                $"'defaultCollections.targetActiveItems' {target} is outside the reachable range [{lowest}, {highest}]: " +
                $"largest totals {largestSum}, and the other {drawCount} non-empty members (nonEmptyRate × {memberCount} " +
                $"eligible members, minus largest) hold 1 to {cap} items each (the highest sizes max, or 1 without sizes). " +
                "Change targetActiveItems, nonEmptyRate, largest or sizes.");
        }
    }

    /// <summary>
    /// Scales <paramref name="drawn"/> so it adds up to <paramref name="target"/> minus the <c>largest</c> entries,
    /// each entry staying between 1 and <paramref name="cap"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when scaling stops short of the target, which <see cref="SizeHistogram.ScaleTo"/> does silently when
    /// a bucket with <c>min</c> 0 drew zeros it never moves.
    /// </exception>
    private static void ScaleToTarget(int[] drawn, int target, int largestSum, int cap)
    {
        var drawnTarget = target - largestSum;
        SizeHistogram.ScaleTo(drawn, drawnTarget, cap);

        if (drawn.Sum() != drawnTarget)
        {
            throw new InvalidOperationException(
                $"'defaultCollections.targetActiveItems' {target} was not reached: scaling stopped at " +
                $"{largestSum + drawn.Sum()} because {drawn.Count(d => d == 0)} drawn sizes are 0. " +
                "Raise the min of the sizes buckets that start at 0.");
        }
    }

    /// <summary>
    /// Trashed items per member on top of the active ones (<see cref="SeedPresetDefaultCollections.DeletedRate"/>
    /// of the active count), so the active counts keep their shape. <see cref="DefaultCollectionSizing.Sampled"/>
    /// rounds each member randomly; <see cref="DefaultCollectionSizing.Quantile"/> apportions an exact total.
    /// </summary>
    private int[] ComputeTrashCounts(int[] activeCounts, Random random)
    {
        var deletedRate = options.DeletedRate ?? 0;
        return sizing switch
        {
            DefaultCollectionSizing.Sampled => RoundTrashRandomly(activeCounts, deletedRate, random),
            DefaultCollectionSizing.Quantile => ApportionTrash(activeCounts, deletedRate),
        };
    }

    private static int[] RoundTrashRandomly(int[] activeCounts, double deletedRate, Random random)
    {
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

    /// <summary>
    /// Largest-remainder apportionment of <c>round(sum(active) × rate)</c>: every member gets the floor or ceiling of
    /// its own share, ties broken by active count then index, so no <see cref="Random"/> is involved.
    /// </summary>
    /// <remarks>
    /// <c>decimal</c> keeps shares like 40 × 0.075 exactly 3 instead of a binary neighbour that would floor to 2.
    /// </remarks>
    private static int[] ApportionTrash(int[] activeCounts, double deletedRate)
    {
        var rate = (decimal)deletedRate;
        var shares = activeCounts.Select(active => active * rate).ToArray();
        var trashCounts = shares.Select(share => (int)decimal.Floor(share)).ToArray();
        var total = (int)Math.Round(activeCounts.Sum() * rate);

        var roundedUp = Enumerable.Range(0, activeCounts.Length)
            .OrderByDescending(u => shares[u] - trashCounts[u])
            .ThenByDescending(u => activeCounts[u])
            .ThenBy(u => u)
            .Take(total - trashCounts.Sum());
        foreach (var u in roundedUp)
        {
            trashCounts[u]++;
        }

        return trashCounts;
    }
}
