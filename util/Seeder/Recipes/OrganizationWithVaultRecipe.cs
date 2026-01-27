using AutoMapper;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Data;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Factories;
using Bit.Seeder.Options;
using LinqToDB.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
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
    RustSdkService sdkService,
    IPasswordHasher<User> passwordHasher)
{
    private readonly CollectionSeeder _collectionSeeder = new(sdkService);
    private readonly CipherSeeder _cipherSeeder = new(sdkService);

    /// <summary>
    /// Seeds an organization with users, collections, groups, and encrypted ciphers.
    /// </summary>
    /// <param name="options">Options specifying what to seed.</param>
    /// <returns>The organization ID.</returns>
    public Guid Seed(OrganizationVaultOptions options)
    {
        var seats = Math.Max(options.Users + 1, 1000);
        var orgKeys = sdkService.GenerateOrganizationKeys();

        // Create organization via factory
        var organization = OrganizationSeeder.CreateEnterprise(options.Name, options.Domain, seats);
        organization.PublicKey = orgKeys.PublicKey;
        organization.PrivateKey = orgKeys.PrivateKey;

        // Create owner user via factory
        var ownerUser = UserSeeder.CreateUserWithSdkKeys($"owner@{options.Domain}", sdkService, passwordHasher);
        var ownerOrgKey = sdkService.GenerateUserOrganizationKey(ownerUser.PublicKey!, orgKeys.Key);
        var ownerOrgUser = organization.CreateOrganizationUserWithKey(
            ownerUser, OrganizationUserType.Owner, OrganizationUserStatusType.Confirmed, ownerOrgKey);

        // Create member users via factory
        var memberUsers = new List<User>();
        var memberOrgUsers = new List<OrganizationUser>();
        var useRealisticMix = options.RealisticStatusMix && options.Users >= 10;

        for (var i = 0; i < options.Users; i++)
        {
            var memberUser = UserSeeder.CreateUserWithSdkKeys($"user{i}@{options.Domain}", sdkService, passwordHasher);
            memberUsers.Add(memberUser);

            var status = useRealisticMix
                ? GetRealisticStatus(i, options.Users)
                : OrganizationUserStatusType.Confirmed;

            var memberOrgKey = (status == OrganizationUserStatusType.Confirmed ||
                                status == OrganizationUserStatusType.Revoked)
                ? sdkService.GenerateUserOrganizationKey(memberUser.PublicKey!, orgKeys.Key)
                : null;

            memberOrgUsers.Add(organization.CreateOrganizationUserWithKey(
                memberUser, OrganizationUserType.User, status, memberOrgKey));
        }

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
        CreateCiphers(organization.Id, orgKeys.Key, collectionIds, options.Ciphers, options.UsernamePattern, options.PasswordStrength);

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
                .Select(unit => _collectionSeeder.CreateCollection(organizationId, orgKeyBase64, unit.Name))
                .ToList();
        }
        else
        {
            collections = [_collectionSeeder.CreateCollection(organizationId, orgKeyBase64, "Default Collection")];
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
                        .Select(j => CollectionSeeder.CreateCollectionUser(
                            collections[(userIndex + j) % collections.Count].Id,
                            orgUserId,
                            readOnly: j > 0,
                            manage: j == 0));
                })
                .ToList();
            db.BulkCopy(collectionUsers);
        }

        return collections.Select(c => c.Id).ToList();
    }

    private void CreateGroups(Guid organizationId, int groupCount, List<Guid> orgUserIds)
    {
        var groupList = Enumerable.Range(0, groupCount)
            .Select(i => GroupSeeder.CreateGroup(organizationId, $"Group {i + 1}"))
            .ToList();

        db.BulkCopy(groupList);

        // Create group-user relationships (round-robin assignment)
        if (groupList.Count > 0 && orgUserIds.Count > 0)
        {
            var groupUsers = orgUserIds
                .Select((orgUserId, i) => GroupSeeder.CreateGroupUser(
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
        UsernamePatternType usernamePattern,
        PasswordStrength passwordStrength)
    {
        var companies = Companies.All;
        var usernameGenerator = new UsernameGenerator(organizationId.GetHashCode(), usernamePattern);

        var cipherList = Enumerable.Range(0, cipherCount)
            .Select(i =>
            {
                var company = companies[i % companies.Length];
                return _cipherSeeder.CreateOrganizationLoginCipher(
                    organizationId,
                    orgKeyBase64,
                    name: $"{company.Name} ({company.Category})",
                    username: usernameGenerator.GenerateVaried(company, i),
                    password: Passwords.GetPassword(passwordStrength, i),
                    uri: $"https://{company.Domain}");
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

                return new[] { primary };
            }).ToList();

            db.BulkCopy(collectionCiphers);
        }
    }

    /// <summary>
    /// Returns a realistic user status based on index position.
    /// Distribution: 85% Confirmed, 5% Invited, 5% Accepted, 5% Revoked.
    /// </summary>
    private static OrganizationUserStatusType GetRealisticStatus(int index, int totalUsers)
    {
        // Calculate bucket boundaries
        var confirmedCount = (int)(totalUsers * 0.85);
        var invitedCount = (int)(totalUsers * 0.05);
        var acceptedCount = (int)(totalUsers * 0.05);
        // Revoked gets the remainder

        if (index < confirmedCount)
        {
            return OrganizationUserStatusType.Confirmed;
        }

        if (index < confirmedCount + invitedCount)
        {
            return OrganizationUserStatusType.Invited;
        }

        if (index < confirmedCount + invitedCount + acceptedCount)
        {
            return OrganizationUserStatusType.Accepted;
        }

        return OrganizationUserStatusType.Revoked;
    }
}
