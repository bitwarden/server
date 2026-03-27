using Bit.Core.Entities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Factories.Vault;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Loads cipher items from a fixture and creates encrypted cipher entities.
/// Supports both organization ciphers (encrypted with org key, assigned to collections)
/// and personal ciphers (encrypted with user key, no collections).
/// </summary>
internal sealed class CreateCiphersStep : IStep
{
    private readonly string _fixtureName;
    private readonly bool _skipCollectionAssignment;
    private readonly bool _personal;

    private CreateCiphersStep(string fixtureName, bool skipCollectionAssignment, bool personal)
    {
        _fixtureName = fixtureName;
        _skipCollectionAssignment = skipCollectionAssignment;
        _personal = personal;
    }

    internal static CreateCiphersStep ForOrganization(string fixtureName, bool skipCollectionAssignment = false) =>
        new(fixtureName, skipCollectionAssignment, personal: false);

    internal static CreateCiphersStep ForPersonalVault(string fixtureName) =>
        new(fixtureName, skipCollectionAssignment: true, personal: true);

    public void Execute(SeederContext context)
    {
        string encryptionKey;
        Guid? organizationId = null;
        Guid? userId = null;

        if (_personal)
        {
            var userDigest = context.Registry.UserDigests[0];
            encryptionKey = userDigest.SymmetricKey;
            userId = userDigest.UserId;
        }
        else
        {
            organizationId = context.RequireOrgId();
            encryptionKey = context.RequireOrgKey();
        }

        var seedFile = context.GetSeedReader().Read<SeedFile>($"ciphers.{_fixtureName}");
        var collectionIds = context.Registry.CollectionIds;

        var ciphers = new List<Cipher>(seedFile.Items.Count);
        var collectionCiphers = new List<CollectionCipher>();

        for (var i = 0; i < seedFile.Items.Count; i++)
        {
            var item = seedFile.Items[i];
            var options = CipherSeed.FromSeedItem(item) with
            {
                EncryptionKey = encryptionKey,
                OrganizationId = organizationId,
                UserId = userId
            };
            var cipher = options.Type switch
            {
                CipherType.Login => LoginCipherSeeder.Create(options),
                CipherType.Card => CardCipherSeeder.Create(options),
                CipherType.Identity => IdentityCipherSeeder.Create(options),
                CipherType.SecureNote => SecureNoteCipherSeeder.Create(options),
                CipherType.SSHKey => SshKeyCipherSeeder.Create(options),
                _ => throw new ArgumentOutOfRangeException(nameof(options), $"Unsupported cipher type: {options.Type}")
            };

            if (item.Favorite == true && userId.HasValue)
            {
                cipher.Favorites = CipherComposer.BuildFavoritesJson([userId.Value]);
            }

            if (item.Reprompt == 1)
            {
                cipher.Reprompt = Core.Vault.Enums.CipherRepromptType.Password;
            }

            ciphers.Add(cipher);

            if (context.Registry.FixtureCipherNameToId.ContainsKey(item.Name))
            {
                throw new InvalidOperationException(
                    $"Duplicate cipher name '{item.Name}' in fixture '{_fixtureName}'. " +
                    "Cipher names must be unique for folder and favorite assignments.");
            }

            context.Registry.FixtureCipherNameToId[item.Name] = cipher.Id;

            // Collection assignment (round-robin, skipped for personal vaults or when collectionAssignments handles it)
            if (_skipCollectionAssignment || collectionIds.Count <= 0)
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
        context.CollectionCiphers.AddRange(collectionCiphers);
    }
}
