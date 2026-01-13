using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using LinqToDB.EntityFrameworkCore;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Creates an organization with an owner and optional additional users.
/// </summary>
public class OrganizationWithUsersScene(
    DatabaseContext db,
    UserSeeder userSeeder,
    RustSdkService sdkService) : IScene<OrganizationWithUsersScene.Request, OrganizationWithUsersScene.Response>
{
    public class Request
    {
        /// <summary>
        /// The name of the organization.
        /// </summary>
        [Required]
        public required string Name { get; set; }

        /// <summary>
        /// The domain for the organization (used for billing email and user emails).
        /// </summary>
        [Required]
        public required string Domain { get; set; }

        /// <summary>
        /// The number of additional users to create (beyond the owner).
        /// </summary>
        public int Users { get; set; } = 0;

        /// <summary>
        /// The status to assign to additional users. Defaults to Confirmed.
        /// Valid values: Invited (0), Accepted (1), Confirmed (2), Revoked (-1)
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public OrganizationUserStatusType UsersStatus { get; set; } = OrganizationUserStatusType.Confirmed;
    }

    public class Response
    {
        public Guid OwnerUserId { get; set; }
        public string OwnerEmail { get; set; } = string.Empty;
        public string MasterPassword { get; set; } = "asdfasdfasdf";
    }

    public async Task<SceneResult<Response>> SeedAsync(Request request)
    {
        var seats = Math.Max(request.Users + 1, 1000);

        // Generate organization keys dynamically
        var orgKeys = sdkService.GenerateOrganizationKeys();
        var organization = OrganizationSeeder.CreateEnterprise(request.Name, request.Domain, seats);
        // Overwrite hardcoded keys with dynamic ones so they match the dynamic user keys
        organization.PublicKey = orgKeys.PublicKey;
        organization.PrivateKey = orgKeys.PrivateKey;

        // Create owner user (UserSeeder returns Core.Entities.User, we need to convert to EF model)
        // EmailVerified=true to ensure all features are available
        var ownerCoreUser = userSeeder.CreateUser($"owner@{request.Domain}", emailVerified: true);
        var ownerUser = ToEfUser(ownerCoreUser);

        // Generate org user key dynamically so it matches the dynamic user and org keys
        var ownerOrgKey = sdkService.GenerateUserOrganizationKey(ownerCoreUser.PublicKey!, orgKeys.Key);
        var ownerOrgUser = CreateOrganizationUser(
            organization.Id,
            ownerUser.Id,
            null,
            ownerOrgKey,
            OrganizationUserType.Owner,
            OrganizationUserStatusType.Confirmed);

        var additionalUsers = new List<User>();
        var additionalOrgUsers = new List<OrganizationUser>();

        for (var i = 0; i < request.Users; i++)
        {
            var additionalCoreUser = userSeeder.CreateUser($"user{i}@{request.Domain}");
            var additionalUser = ToEfUser(additionalCoreUser);
            additionalUsers.Add(additionalUser);

            // Generate org user key dynamically for each user
            var userOrgKey = request.UsersStatus == OrganizationUserStatusType.Confirmed ||
                             request.UsersStatus == OrganizationUserStatusType.Revoked
                ? sdkService.GenerateUserOrganizationKey(additionalCoreUser.PublicKey!, orgKeys.Key)
                : null;

            var orgUser = CreateOrganizationUser(
                organization.Id,
                request.UsersStatus == OrganizationUserStatusType.Invited ? null : additionalUser.Id,
                request.UsersStatus == OrganizationUserStatusType.Invited ? additionalUser.Email : null,
                userOrgKey,
                OrganizationUserType.User,
                request.UsersStatus);

            additionalOrgUsers.Add(orgUser);
        }

        db.Organizations.Add(organization);
        db.Users.Add(ownerUser);
        db.OrganizationUsers.Add(ownerOrgUser);

        await db.SaveChangesAsync();

        if (additionalUsers.Count > 0)
        {
            await db.BulkCopyAsync(additionalUsers);
            await db.BulkCopyAsync(additionalOrgUsers);
        }

        // Note: Collections are not created during seeding because collection names must be
        // encrypted with the organization key. Users can create collections through the UI.
        // The organization will still appear in the import dropdown because Owners have
        // canAccessImport permission.

        var response = new Response
        {
            OwnerUserId = ownerUser.Id,
            OwnerEmail = ownerUser.Email!
        };

        return new SceneResult<Response>(response, new Dictionary<string, string?>());
    }

    private static User ToEfUser(Core.Entities.User coreUser)
    {
        return new User
        {
            Id = coreUser.Id,
            Email = coreUser.Email,
            EmailVerified = coreUser.EmailVerified,
            MasterPassword = coreUser.MasterPassword,
            SecurityStamp = coreUser.SecurityStamp,
            Key = coreUser.Key,
            PublicKey = coreUser.PublicKey,
            PrivateKey = coreUser.PrivateKey,
            Premium = coreUser.Premium,
            ApiKey = coreUser.ApiKey,
            Kdf = coreUser.Kdf,
            KdfIterations = coreUser.KdfIterations,
            CreationDate = coreUser.CreationDate,
            RevisionDate = coreUser.RevisionDate
        };
    }

    private static OrganizationUser CreateOrganizationUser(
        Guid organizationId,
        Guid? userId,
        string? email,
        string? encryptedOrgKey,
        OrganizationUserType type,
        OrganizationUserStatusType status)
    {
        return new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Email = email,
            Key = encryptedOrgKey,
            Type = type,
            Status = status
        };
    }

}
