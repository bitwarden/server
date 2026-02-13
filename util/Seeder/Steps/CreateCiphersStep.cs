using Bit.Core.Entities;
using Bit.Core.Vault.Entities;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Loads cipher items from a fixture and creates encrypted cipher entities.
/// </summary>
internal sealed class CreateCiphersStep(string fixtureName) : IStep
{
    public void Execute(SeederContext context)
    {
        var orgId = context.RequireOrgId();
        var orgKey = context.RequireOrgKey();
        var seedFile = context.GetSeedReader().Read<SeedFile>($"ciphers.{fixtureName}");
        var collectionIds = context.Registry.CollectionIds;

        var ciphers = new List<Cipher>();
        var collectionCiphers = new List<CollectionCipher>();

        for (var i = 0; i < seedFile.Items.Count; i++)
        {
            var item = seedFile.Items[i];
            var cipher = item.Type switch
            {
                "login" => LoginCipherSeeder.CreateFromSeed(orgKey, item, organizationId: orgId),
                "card" => CardCipherSeeder.CreateFromSeed(orgKey, item, organizationId: orgId),
                "identity" => IdentityCipherSeeder.CreateFromSeed(orgKey, item, organizationId: orgId),
                "secureNote" => SecureNoteCipherSeeder.CreateFromSeed(orgKey, item, organizationId: orgId),
                _ => throw new InvalidOperationException($"Unknown cipher type: {item.Type}")
            };

            ciphers.Add(cipher);

            // Collection assignment (mirrors GenerateCiphersStep logic)
            if (collectionIds.Count <= 0)
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
