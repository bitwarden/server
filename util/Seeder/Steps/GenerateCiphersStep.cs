using Bit.Core.Entities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Data;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Factories;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates N random cipher entities using the deterministic <see cref="GeneratorContext"/>.
/// </summary>
/// <remarks>
/// Requires <see cref="InitGeneratorStep"/> to have run first. Picks cipher types (login, card,
/// identity, secureNote, sshKey) from a configurable distribution, delegates to the existing
/// cipher factories, and assigns each cipher to collections round-robin. Designed for load
/// testing scenarios where you need thousands of realistic vault items.
/// </remarks>
/// <seealso cref="InitGeneratorStep"/>
/// <seealso cref="CreateCiphersStep"/>
internal sealed class GenerateCiphersStep(
    int count,
    Distribution<CipherType>? typeDist = null,
    Distribution<PasswordStrength>? pwDist = null,
    bool assignFolders = false) : IStep
{
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
        var typeDistribution = typeDist ?? CipherTypeDistributions.Realistic;
        var passwordDistribution = pwDist ?? PasswordDistributions.Realistic;
        var companies = Companies.All;

        var userDigests = assignFolders ? context.Registry.UserDigests : null;
        var userFolderIds = assignFolders ? context.Registry.UserFolderIds : null;

        var ciphers = new List<Cipher>(count);
        var cipherIds = new List<Guid>(count);
        var collectionCiphers = new List<CollectionCipher>();

        for (var i = 0; i < count; i++)
        {
            var cipherType = typeDistribution.Select(i, count);
            var cipher = cipherType switch
            {
                CipherType.Login => CipherComposer.ComposeLogin(i, orgKey, companies, generator, passwordDistribution, organizationId: orgId),
                CipherType.Card => CipherComposer.ComposeCard(i, orgKey, generator, organizationId: orgId),
                CipherType.Identity => CipherComposer.ComposeIdentity(i, orgKey, generator, organizationId: orgId),
                CipherType.SecureNote => CipherComposer.ComposeSecureNote(i, orgKey, generator, organizationId: orgId),
                CipherType.SSHKey => CipherComposer.ComposeSshKey(i, orgKey, organizationId: orgId),
                _ => throw new ArgumentException($"Unsupported cipher type: {cipherType}")
            };

            if (userDigests is { Count: > 0 } && userFolderIds is not null)
            {
                var userDigest = userDigests[i % userDigests.Count];
                CipherComposer.AssignFolder(cipher, userDigest.UserId, i, userFolderIds);
            }

            ciphers.Add(cipher);
            cipherIds.Add(cipher.Id);

            // Collection assignment
            if (collectionIds.Count == 0)
            {
                continue;
            }

            collectionCiphers.Add(new CollectionCipher
            {
                CipherId = cipher.Id,
                CollectionId = collectionIds[i % collectionIds.Count]
            });

            // Every 3rd cipher gets assigned to an additional collection
            if (i % 3 == 0 && collectionIds.Count > 1)
            {
                collectionCiphers.Add(new CollectionCipher
                {
                    CipherId = cipher.Id,
                    CollectionId = collectionIds[(i + 1) % collectionIds.Count]
                });
            }
        }

        context.Ciphers.AddRange(ciphers);
        context.Registry.CipherIds.AddRange(cipherIds);
        context.CollectionCiphers.AddRange(collectionCiphers);
    }
}
