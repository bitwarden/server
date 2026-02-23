using AutoMapper;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Data;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Generators;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Factories;
using Bit.Seeder.Options;
using Bit.Seeder.Services;
using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using EfFolder = Bit.Infrastructure.EntityFramework.Vault.Models.Folder;
using EfOrganization = Bit.Infrastructure.EntityFramework.AdminConsole.Models.Organization;
using EfOrganizationUser = Bit.Infrastructure.EntityFramework.Models.OrganizationUser;
using EfUser = Bit.Infrastructure.EntityFramework.Models.User;

namespace Bit.Seeder.Recipes;

/// <summary>
/// Seeds an organization with users, collections, groups, and encrypted ciphers.
/// </summary>
/// <remarks>
/// This recipe creates a complete organization with vault data in a single operation.
/// All entity creation is delegated to factories. Users can log in with their email
/// and password "asdfasdfasdf". Organization and user keys are generated dynamically.
/// </remarks>
public class OrganizationWithVaultRecipe(
    DatabaseContext db,
    IMapper mapper,
    IPasswordHasher<User> passwordHasher,
    IManglerService manglerService)
{
    private const int _minimumOrgSeats = 1000;

    private GeneratorContext _ctx = null!;

    /// <summary>
    /// Tracks a user with their symmetric key for folder encryption.
    /// </summary>
    private record UserWithKey(User User, string SymmetricKey);

    /// <summary>
    /// Seeds an organization with users, collections, groups, and encrypted ciphers.
    /// </summary>
    /// <param name="options">Options specifying what to seed.</param>
    /// <returns>The organization ID.</returns>
    public Guid Seed(OrganizationVaultOptions options)
    {
        _ctx = GeneratorContext.FromOptions(options);
        var password = options.Password ?? UserSeeder.DefaultPassword;

        var seats = Math.Max(options.Users + 1, _minimumOrgSeats);
        var orgKeys = RustSdkService.GenerateOrganizationKeys();

        // Create organization via factory
        var organization = OrganizationSeeder.Create(
            options.Name, options.Domain, seats, manglerService, orgKeys.PublicKey, orgKeys.PrivateKey, options.PlanType);

        // Create owner user via factory
        var ownerEmail = $"owner@{options.Domain}";
        var mangledOwnerEmail = manglerService.Mangle(ownerEmail);
        var ownerKeys = RustSdkService.GenerateUserKeys(mangledOwnerEmail, password);
        var ownerUser = UserSeeder.Create(mangledOwnerEmail, passwordHasher, manglerService, keys: ownerKeys, password: password);

        var ownerOrgKey = RustSdkService.GenerateUserOrganizationKey(ownerUser.PublicKey!, orgKeys.Key);
        var ownerOrgUser = organization.CreateOrganizationUserWithKey(
            ownerUser, OrganizationUserType.Owner, OrganizationUserStatusType.Confirmed, ownerOrgKey);

        // Create member users via factory, retaining keys for folder encryption
        var memberUsersWithKeys = new List<UserWithKey>();
        var memberOrgUsers = new List<OrganizationUser>();
        var useRealisticMix = options.RealisticStatusMix && options.Users >= 10;

        for (var i = 0; i < options.Users; i++)
        {
            var email = $"user{i}@{options.Domain}";
            var mangledEmail = manglerService.Mangle(email);
            var userKeys = RustSdkService.GenerateUserKeys(mangledEmail, password);
            var memberUser = UserSeeder.Create(mangledEmail, passwordHasher, manglerService, keys: userKeys, password: password);
            memberUsersWithKeys.Add(new UserWithKey(memberUser, userKeys.Key));

            var status = useRealisticMix
                ? UserStatusDistributions.Realistic.Select(i, options.Users)
                : OrganizationUserStatusType.Confirmed;

            var memberOrgKey = (status == OrganizationUserStatusType.Confirmed ||
                                status == OrganizationUserStatusType.Revoked)
                ? RustSdkService.GenerateUserOrganizationKey(memberUser.PublicKey!, orgKeys.Key)
                : null;

            memberOrgUsers.Add(organization.CreateOrganizationUserWithKey(
                memberUser, OrganizationUserType.User, status, memberOrgKey));
        }

        var memberUsers = memberUsersWithKeys.Select(uwk => uwk.User).ToList();

        // Persist organization and users
        db.Add(mapper.Map<EfOrganization>(organization));
        db.Add(mapper.Map<EfUser>(ownerUser));
        db.Add(mapper.Map<EfOrganizationUser>(ownerOrgUser));

        var efMemberUsers = memberUsers.Select(u => mapper.Map<EfUser>(u)).ToList();
        var efMemberOrgUsers = memberOrgUsers.Select(ou => mapper.Map<EfOrganizationUser>(ou)).ToList();
        db.BulkCopy(efMemberUsers);
        db.BulkCopy(efMemberOrgUsers);
        db.SaveChanges();

        // Get confirmed org user IDs for collection/group relationships
        var confirmedOrgUserIds = memberOrgUsers
            .Where(ou => ou.Status == OrganizationUserStatusType.Confirmed)
            .Select(ou => ou.Id)
            .Prepend(ownerOrgUser.Id)
            .ToList();

        var collectionIds = CreateCollections(organization.Id, orgKeys.Key, options.StructureModel, confirmedOrgUserIds);
        CreateGroups(organization.Id, options.Groups, confirmedOrgUserIds);
        CreateCiphers(organization.Id, orgKeys.Key, collectionIds, options.Ciphers, options.PasswordDistribution, options.CipherTypeDistribution);
        CreateFolders(memberUsersWithKeys);

        return organization.Id;
    }

    private List<Guid> CreateCollections(
        Guid organizationId,
        string orgKeyBase64,
        OrgStructureModel? structureModel,
        List<Guid> orgUserIds)
    {
        List<Collection> collections;

        if (structureModel.HasValue)
        {
            var structure = OrgStructures.GetStructure(structureModel.Value);
            collections = structure.Units
                .Select(unit => CollectionSeeder.Create(organizationId, orgKeyBase64, unit.Name))
                .ToList();
        }
        else
        {
            collections = [CollectionSeeder.Create(organizationId, orgKeyBase64, "Default Collection")];
        }

        db.BulkCopy(collections);

        // Create collection-user relationships
        if (collections.Count > 0 && orgUserIds.Count > 0)
        {
            var collectionUsers = orgUserIds
                .SelectMany((orgUserId, userIndex) =>
                {
                    var maxAssignments = Math.Min((userIndex % 3) + 1, collections.Count);
                    return Enumerable.Range(0, maxAssignments)
                        .Select(j => CollectionUserSeeder.Create(
                            collections[(userIndex + j) % collections.Count].Id,
                            orgUserId,
                            readOnly: j > 0,
                            manage: j == 0));
                })
                .ToList();
            db.BulkCopy(new BulkCopyOptions { TableName = nameof(CollectionUser) }, collectionUsers);
        }

        return collections.Select(c => c.Id).ToList();
    }

    private void CreateGroups(Guid organizationId, int groupCount, List<Guid> orgUserIds)
    {
        var groupList = Enumerable.Range(0, groupCount)
            .Select(i => GroupSeeder.Create(organizationId, $"Group {i + 1}"))
            .ToList();

        db.BulkCopy(groupList);

        // Create group-user relationships (round-robin assignment)
        if (groupList.Count > 0 && orgUserIds.Count > 0)
        {
            var groupUsers = orgUserIds
                .Select((orgUserId, i) => GroupUserSeeder.Create(
                    groupList[i % groupList.Count].Id,
                    orgUserId))
                .ToList();
            db.BulkCopy(groupUsers);
        }
    }

    private void CreateCiphers(
        Guid organizationId,
        string orgKeyBase64,
        List<Guid> collectionIds,
        int cipherCount,
        Distribution<PasswordStrength> passwordDistribution,
        Distribution<CipherType> typeDistribution)
    {
        if (cipherCount == 0)
        {
            return;
        }

        var companies = Companies.All;

        var cipherList = Enumerable.Range(0, cipherCount)
            .Select(i =>
            {
                var cipherType = typeDistribution.Select(i, cipherCount);
                return cipherType switch
                {
                    CipherType.Login => CreateLoginCipher(i, organizationId, orgKeyBase64, companies, cipherCount, passwordDistribution),
                    CipherType.Card => CreateCardCipher(i, organizationId, orgKeyBase64),
                    CipherType.Identity => CreateIdentityCipher(i, organizationId, orgKeyBase64),
                    CipherType.SecureNote => CreateSecureNoteCipher(i, organizationId, orgKeyBase64),
                    CipherType.SSHKey => CreateSshKeyCipher(i, organizationId, orgKeyBase64),
                    _ => throw new ArgumentException($"Unsupported cipher type: {cipherType}")
                };
            })
            .ToList();

        db.BulkCopy(cipherList);

        // Create cipher-collection relationships
        if (cipherList.Count > 0 && collectionIds.Count > 0)
        {
            var collectionCiphers = cipherList.SelectMany((cipher, i) =>
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
    }
    private Cipher CreateLoginCipher(
        int index,
        Guid organizationId,
        string orgKeyBase64,
        Company[] companies,
        int cipherCount,
        Distribution<PasswordStrength> passwordDistribution)
    {
        var company = companies[index % companies.Length];
        return LoginCipherSeeder.Create(
            orgKeyBase64,
            name: $"{company.Name} ({company.Category})",
            organizationId: organizationId,
            username: _ctx.Username.GenerateByIndex(index, totalHint: _ctx.CipherCount, domain: company.Domain),
            password: Passwords.GetPassword(index, cipherCount, passwordDistribution),
            uri: $"https://{company.Domain}");
    }

    private Cipher CreateCardCipher(int index, Guid organizationId, string orgKeyBase64)
    {
        var card = _ctx.Card.GenerateByIndex(index);
        return CardCipherSeeder.Create(
            orgKeyBase64,
            name: $"{card.CardholderName}'s {card.Brand}",
            card: card,
            organizationId: organizationId);
    }

    private Cipher CreateIdentityCipher(int index, Guid organizationId, string orgKeyBase64)
    {
        var identity = _ctx.Identity.GenerateByIndex(index);
        var name = $"{identity.FirstName} {identity.LastName}";
        if (!string.IsNullOrEmpty(identity.Company))
        {
            name += $" ({identity.Company})";
        }
        return IdentityCipherSeeder.Create(
            orgKeyBase64,
            name: name,
            identity: identity,
            organizationId: organizationId);
    }

    private Cipher CreateSecureNoteCipher(int index, Guid organizationId, string orgKeyBase64)
    {
        var (name, notes) = _ctx.SecureNote.GenerateByIndex(index);
        return SecureNoteCipherSeeder.Create(
            orgKeyBase64,
            name: name,
            organizationId: organizationId,
            notes: notes);
    }

    private Cipher CreateSshKeyCipher(int index, Guid organizationId, string orgKeyBase64)
    {
        var sshKey = SshKeyDataGenerator.GenerateByIndex(index);
        return SshKeyCipherSeeder.Create(
            orgKeyBase64,
            name: $"SSH Key {index + 1}",
            sshKey: sshKey,
            organizationId: organizationId);
    }

    /// <summary>
    /// Creates personal vault folders for users with realistic distribution.
    /// </summary>
    private void CreateFolders(List<UserWithKey> usersWithKeys)
    {
        if (usersWithKeys.Count == 0)
        {
            return;
        }

        var allFolders = usersWithKeys
            .SelectMany((uwk, userIndex) =>
            {
                var folderCount = GetFolderCountForUser(userIndex, usersWithKeys.Count, _ctx.Seed);
                return Enumerable.Range(0, folderCount)
                    .Select(folderIndex => FolderSeeder.Create(
                        uwk.User.Id,
                        uwk.SymmetricKey,
                        _ctx.Folder.GetFolderName(userIndex * 15 + folderIndex)));
            })
            .ToList();

        if (allFolders.Count > 0)
        {
            var efFolders = allFolders.Select(f => mapper.Map<EfFolder>(f)).ToList();
            db.BulkCopy(efFolders);
        }
    }

    private static int GetFolderCountForUser(int userIndex, int totalUsers, int seed)
    {
        var (min, max) = FolderCountDistributions.Realistic.Select(userIndex, totalUsers);
        return GetDeterministicValueInRange(userIndex, seed, min, max);
    }

    /// <summary>
    /// Returns a deterministic value in [min, max) based on index and seed.
    /// </summary>
    private static int GetDeterministicValueInRange(int index, int seed, int min, int max)
    {
        unchecked
        {
            var hash = seed;
            hash = hash * 397 ^ index;
            hash = hash * 397 ^ min;
            var range = max - min;
            return min + ((hash % range) + range) % range;
        }
    }
}
