using Bit.Core.Entities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Data;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Factories;
using Bit.Seeder.Options;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates N random cipher entities using the deterministic <see cref="GeneratorContext"/>.
/// </summary>
/// <remarks>
/// Requires <see cref="InitGeneratorStep"/> to have run first. Picks cipher types (login, card,
/// identity, secureNote, sshKey) from a configurable distribution, delegates to the existing
/// cipher factories, and assigns ciphers to collections (configurable via density profile). Designed for load
/// testing scenarios where you need thousands of realistic vault items.
/// </remarks>
/// <seealso cref="InitGeneratorStep"/>
/// <seealso cref="CreateCiphersStep"/>
internal sealed class GenerateCiphersStep(
    int count,
    Distribution<CipherType>? typeDist = null,
    Distribution<PasswordStrength>? pwDist = null,
    bool assignFolders = false,
    DensityProfile? density = null,
    int repromptEveryNthCipher = 0) : IStep
{
    private readonly DensityProfile? _density = density;

    public void Execute(SeederContext context)
    {
        if (count == 0)
        {
            return;
        }

        var generator = context.RequireGenerator();

        var orgId = context.RequireOrgId();
        var orgKey = context.RequireOrgKey();
        var collectionIds = context.Registry.CollectionIds;
        var typeDistribution = typeDist ?? _density?.CipherTypeDistribution ?? CipherTypeDistributions.Realistic;
        var passwordDistribution = pwDist ?? PasswordDistributions.Realistic;
        var companies = Companies.All;

        var userDigests = context.Registry.UserDigests;
        var userFolderIds = assignFolders ? context.Registry.UserFolderIds : null;
        var canArchive = userDigests is { Count: > 0 };

        // Archive and delete targets/ceilings are computed independently of GeneratePersonalCiphersStep's
        // personal-cipher pool — each pool enforces its own MaxArchivedCiphers/MaxDeletedCiphers cap
        // rather than sharing one combined budget across steps. Org ciphers are archived "for" a
        // round-robin-selected user (there's no single owning user like a personal cipher has), so
        // archiving requires at least one user to exist.
        var (archivedOrgTarget, deletedOrgTarget, bothOrgTarget, deletedOnlyOrgTarget) = ArchiveDeleteDistribution.ComputeTargets(
            count,
            _density?.ArchivedCipherRate ?? 0, _density?.DeletedCipherRate ?? 0, _density?.ArchivedAndDeletedOverlapRate ?? 0,
            _density?.MaxArchivedCiphers ?? 0, _density?.MaxDeletedCiphers ?? 0,
            canArchive);

        var selection = ArchiveDeleteDistribution.Select(count, archivedOrgTarget, bothOrgTarget, deletedOnlyOrgTarget);

        // CreateOwnerStep always adds the Owner to UserDigests before CreateUsersStep runs, so
        // userDigests[0] is the Owner — meaning the first archived cipher (position 0) is always
        // archived for the Owner. Positions are precomputed (not indexed by the raw loop variable i)
        // so the round-robin cycles through every user regardless of how the archived target divides.
        var archivedUserPositions = canArchive
            ? ArchiveDeleteDistribution.AssignRoundRobinUserPositions(selection.ArchivedOrder, userDigests.Count)
            : new Dictionary<int, int>();

        var ciphers = new List<Cipher>(count);
        var cipherIds = new List<Guid>(count);
        var collectionCiphers = new List<CollectionCipher>(count + count / 3);

        for (var i = 0; i < count; i++)
        {
            var cipherType = typeDistribution.Select(i, count);
            var reprompt = repromptEveryNthCipher > 0 && i % repromptEveryNthCipher == 0
                ? CipherRepromptType.Password
                : CipherRepromptType.None;
            var cipher = CipherComposer.Compose(i, cipherType, orgKey, companies, generator, passwordDistribution, organizationId: orgId, reprompt: reprompt);

            if (userDigests is { Count: > 0 } && userFolderIds is not null)
            {
                var userDigest = userDigests[i % userDigests.Count];
                CipherComposer.AssignFolder(cipher, userDigest.UserId, i, userFolderIds);
            }

            CipherComposer.AssignArchiveOrDeleteState(cipher, i, selection, idx => userDigests[archivedUserPositions[idx]].UserId);

            ciphers.Add(cipher);
            cipherIds.Add(cipher.Id);
        }

        if (collectionIds.Count > 0)
        {
            if (_density == null)
            {
                for (var i = 0; i < ciphers.Count; i++)
                {
                    collectionCiphers.Add(new CollectionCipher
                    {
                        CipherId = ciphers[i].Id,
                        CollectionId = collectionIds[i % collectionIds.Count]
                    });

                    if (i % 3 == 0 && collectionIds.Count > 1)
                    {
                        collectionCiphers.Add(new CollectionCipher
                        {
                            CipherId = ciphers[i].Id,
                            CollectionId = collectionIds[(i + 1) % collectionIds.Count]
                        });
                    }
                }
            }
            else
            {
                var orphanCount = (int)(count * _density.OrphanCipherRate);
                var nonOrphanCount = count - orphanCount;
                var primaryIndices = new int[nonOrphanCount];

                for (var i = 0; i < nonOrphanCount; i++)
                {
                    int collectionIndex;
                    if (_density.CipherSkew == CipherCollectionSkew.HeavyRight)
                    {
                        // Sqrt curve: later collections accumulate more ciphers (right-heavy skew)
                        var normalized = Math.Pow((double)i / nonOrphanCount, 0.5);
                        collectionIndex = Math.Min((int)(normalized * collectionIds.Count), collectionIds.Count - 1);
                    }
                    else
                    {
                        collectionIndex = i % collectionIds.Count;
                    }

                    primaryIndices[i] = collectionIndex;

                    collectionCiphers.Add(new CollectionCipher
                    {
                        CipherId = ciphers[i].Id,
                        CollectionId = collectionIds[collectionIndex]
                    });
                }

                if (_density.MultiCollectionRate > 0 && collectionIds.Count > 1)
                {
                    var multiCount = (int)(nonOrphanCount * _density.MultiCollectionRate);
                    for (var i = 0; i < multiCount; i++)
                    {
                        var extraCount = 1 + (i % Math.Max(_density.MaxCollectionsPerCipher - 1, 1));
                        extraCount = Math.Min(extraCount, collectionIds.Count - 1);
                        for (var j = 0; j < extraCount; j++)
                        {
                            var secondaryIndex = (primaryIndices[i] + 1 + j) % collectionIds.Count;
                            collectionCiphers.Add(new CollectionCipher
                            {
                                CipherId = ciphers[i].Id,
                                CollectionId = collectionIds[secondaryIndex]
                            });
                        }
                    }
                }
            }

            // Guarantee the Owner always has an archived and a deleted cipher visible in their own
            // vault sync for manual QA. Visibility requires both the lifecycle-state flag and
            // collection access under Flexible Collections — neither the round-robin nor the
            // density-driven collection assignment has any reason to grant both to the same user
            // otherwise.
            if (context.OwnerOrgUser is { } ownerOrgUser)
            {
                var ownerCollectionId = context.CollectionUsers
                    .FirstOrDefault(cu => cu.OrganizationUserId == ownerOrgUser.Id)?.CollectionId;

                if (ownerCollectionId is { } collectionId)
                {
                    if (selection.ArchivedOrder.Count > 0)
                    {
                        EnsureCollectionAssignment(collectionCiphers, ciphers[selection.ArchivedOrder[0]].Id, collectionId);
                    }

                    if (selection.DeletedOnly.Count > 0)
                    {
                        EnsureCollectionAssignment(collectionCiphers, ciphers[selection.DeletedOnly.First()].Id, collectionId);
                    }
                }
            }
        }

        context.Ciphers.AddRange(ciphers);
        context.Registry.CipherIds.AddRange(cipherIds);
        context.CollectionCiphers.AddRange(collectionCiphers);
    }

    private static void EnsureCollectionAssignment(List<CollectionCipher> collectionCiphers, Guid cipherId, Guid collectionId)
    {
        if (!collectionCiphers.Any(cc => cc.CipherId == cipherId && cc.CollectionId == collectionId))
        {
            collectionCiphers.Add(new CollectionCipher { CipherId = cipherId, CollectionId = collectionId });
        }
    }
}
