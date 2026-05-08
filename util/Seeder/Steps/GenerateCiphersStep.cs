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

        var userDigests = assignFolders ? context.Registry.UserDigests : null;
        var userFolderIds = assignFolders ? context.Registry.UserFolderIds : null;

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
        }

        context.Ciphers.AddRange(ciphers);
        context.Registry.CipherIds.AddRange(cipherIds);
        context.CollectionCiphers.AddRange(collectionCiphers);
    }
}
