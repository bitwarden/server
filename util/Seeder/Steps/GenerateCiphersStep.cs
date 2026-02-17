using Bit.Core.Entities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Data;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Generators;
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
    Distribution<PasswordStrength>? pwDist = null) : IStep
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

        var ciphers = new List<Cipher>(count);
        var cipherIds = new List<Guid>(count);
        var collectionCiphers = new List<CollectionCipher>();

        for (var i = 0; i < count; i++)
        {
            var cipherType = typeDistribution.Select(i, count);
            var cipher = cipherType switch
            {
                CipherType.Login => CreateLoginCipher(i, orgId, orgKey, companies, generator, passwordDistribution),
                CipherType.Card => CreateCardCipher(i, orgId, orgKey, generator),
                CipherType.Identity => CreateIdentityCipher(i, orgId, orgKey, generator),
                CipherType.SecureNote => CreateSecureNoteCipher(i, orgId, orgKey, generator),
                CipherType.SSHKey => CreateSshKeyCipher(i, orgId, orgKey),
                _ => throw new ArgumentException($"Unsupported cipher type: {cipherType}")
            };

            ciphers.Add(cipher);
            cipherIds.Add(cipher.Id);

            // Collection assignment
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
        context.Registry.CipherIds.AddRange(cipherIds);
        context.CollectionCiphers.AddRange(collectionCiphers);
    }

    private static Cipher CreateLoginCipher(
        int index,
        Guid organizationId,
        string orgKey,
        Company[] companies,
        GeneratorContext generator,
        Distribution<PasswordStrength> passwordDistribution)
    {
        var company = companies[index % companies.Length];
        return LoginCipherSeeder.Create(
            orgKey,
            name: $"{company.Name} ({company.Category})",
            organizationId: organizationId,
            username: generator.Username.GenerateByIndex(index, totalHint: generator.CipherCount, domain: company.Domain),
            password: Passwords.GetPassword(index, generator.CipherCount, passwordDistribution),
            uri: $"https://{company.Domain}");
    }

    private static Cipher CreateCardCipher(int index, Guid organizationId, string orgKey, GeneratorContext generator)
    {
        var card = generator.Card.GenerateByIndex(index);
        return CardCipherSeeder.Create(
            orgKey,
            name: $"{card.CardholderName}'s {card.Brand}",
            card: card,
            organizationId: organizationId);
    }

    private static Cipher CreateIdentityCipher(int index, Guid organizationId, string orgKey, GeneratorContext generator)
    {
        var identity = generator.Identity.GenerateByIndex(index);
        var name = $"{identity.FirstName} {identity.LastName}";
        if (!string.IsNullOrEmpty(identity.Company))
        {
            name += $" ({identity.Company})";
        }
        return IdentityCipherSeeder.Create(
            orgKey,
            name: name,
            identity: identity,
            organizationId: organizationId);
    }

    private static Cipher CreateSecureNoteCipher(int index, Guid organizationId, string orgKey, GeneratorContext generator)
    {
        var (name, notes) = generator.SecureNote.GenerateByIndex(index);
        return SecureNoteCipherSeeder.Create(
            orgKey,
            name: name,
            organizationId: organizationId,
            notes: notes);
    }

    private static Cipher CreateSshKeyCipher(int index, Guid organizationId, string orgKey)
    {
        var sshKey = SshKeyDataGenerator.GenerateByIndex(index);
        return SshKeyCipherSeeder.Create(
            orgKey,
            name: $"SSH Key {index + 1}",
            sshKey: sshKey,
            organizationId: organizationId);
    }
}
