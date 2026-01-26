using Bit.Core.Entities;
using Bit.Core.Vault.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Data;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Factories;
using LinqToDB.EntityFrameworkCore;

namespace Bit.Seeder.Recipes;

/// <summary>
/// Creates encrypted ciphers for seeding organization vaults.
/// </summary>
/// <remarks>
/// Currently supports:
/// <list type="bullet">
///   <item><description>Login ciphers</description></item>
/// </list>
/// TODO: Add support for Card, Identity, and SecureNote cipher types.
/// </remarks>
public class CiphersRecipe(DatabaseContext db, RustSdkService sdkService)
{
    private readonly CipherSeeder _cipherSeeder = new(sdkService);

    public List<Guid> AddLoginCiphersToOrganization(
        Guid organizationId,
        string orgKeyBase64,
        List<Guid> collectionIds,
        int? count = null,
        bool useEnterpriseUrls = false)
    {
        // Delegate to the new system - Enterprise filter for enterprise URLs, Consumer for popular
        var companyType = useEnterpriseUrls ? CompanyType.Enterprise : CompanyType.Consumer;
        return AddLoginCiphersToOrganization(
            organizationId,
            orgKeyBase64,
            collectionIds,
            count,
            companyType,
            region: null,
            UsernamePatternType.FLast,
            PasswordStrength.Weak);
    }

    public List<Guid> AddLoginCiphersToOrganization(
        Guid organizationId,
        string orgKeyBase64,
        List<Guid> collectionIds,
        int? count,
        CompanyType? companyType,
        GeographicRegion? region,
        UsernamePatternType usernamePattern = UsernamePatternType.FirstDotLast,
        PasswordStrength passwordStrength = PasswordStrength.Strong)
    {
        var companies = Companies.Filter(companyType, region);
        if (companies.Length == 0)
        {
            companies = Companies.All;
        }

        var passwords = Passwords.GetByStrength(passwordStrength);
        var cipherCount = count ?? companies.Length;
        var usernameGenerator = new UsernameGenerator(organizationId.GetHashCode(), usernamePattern, region);

        var ciphers = Enumerable.Range(0, cipherCount)
            .Select(i =>
            {
                var company = companies[i % companies.Length];
                return _cipherSeeder.CreateOrganizationLoginCipher(
                    organizationId,
                    orgKeyBase64,
                    name: $"{company.Name} ({company.Category})",
                    username: usernameGenerator.GenerateVaried(company, i),
                    password: passwords[i % passwords.Length],
                    uri: $"https://{company.Domain}");
            })
            .ToList();

        return SaveCiphersWithCollections(ciphers, collectionIds);
    }

    private List<Guid> SaveCiphersWithCollections(List<Cipher> ciphers, List<Guid> collectionIds)
    {
        if (ciphers.Count == 0)
        {
            return [];
        }

        db.BulkCopy(ciphers);

        if (collectionIds.Count > 0)
        {
            var collectionCiphers = ciphers.SelectMany((cipher, i) =>
            {
                var primary = new CollectionCipher
                {
                    CipherId = cipher.Id,
                    CollectionId = collectionIds[i % collectionIds.Count]
                };

                // Every 3rd cipher gets assigned to an additional collection
                if (i % 3 == 0 && collectionIds.Count > 1)
                {
                    return new[]
                    {
                        primary,
                        new CollectionCipher
                        {
                            CipherId = cipher.Id,
                            CollectionId = collectionIds[(i + 1) % collectionIds.Count]
                        }
                    };
                }

                return [primary];
            }).ToList();

            db.BulkCopy(collectionCiphers);
        }

        return ciphers.Select(c => c.Id).ToList();
    }
}
