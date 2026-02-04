using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Scenes;

public struct EnterpriseOrganizationSceneResult
{
    public Guid OrganizationId { get; init; }
    public string OwnerEmail { get; init; }
    public string OwnerClientSecret { get; init; }
    public string AdminEmail { get; init; }
    public string UserEmail { get; init; }
    public string CustomUserEmail { get; init; }
    public string CustomUser2Email { get; init; }
}

/// <summary>
/// Creates an Enterprise organization with owner, admin, user, and two custom users.
/// Also creates an organization API key for authentication.
/// </summary>
public class EnterpriseOrganizationScene(
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationApiKeyRepository organizationApiKeyRepository,
    IUserRepository userRepository,
    IPasswordHasher<Core.Entities.User> passwordHasher,
    MangleId mangleId) : IScene<EnterpriseOrganizationScene.Request, EnterpriseOrganizationSceneResult>
{
    public class Request
    {
        public string OrganizationName { get; set; } = "Enterprise Test Org";

        [Required]
        public required string OwnerEmail { get; set; }

        [Required]
        public required string AdminEmail { get; set; }

        [Required]
        public required string UserEmail { get; set; }

        [Required]
        public required string CustomUserEmail { get; set; }

        [Required]
        public required string CustomUser2Email { get; set; }
    }

    public async Task<SceneResult<EnterpriseOrganizationSceneResult>> SeedAsync(Request request)
    {
        var domain = MangleId.ExtractDomain(request.OwnerEmail);

        // Generate organization keys
        var orgKeys = RustSdkService.GenerateOrganizationKeys();

        // Create Enterprise organization
        var organization = OrganizationSeeder.CreateEnterprise(
            request.OrganizationName,
            domain,
            seats: 100,
            orgKeys.PublicKey,
            orgKeys.PrivateKey);

        // Create organization API key
        var apiKey = OrganizationApiKeySeeder.CreateApiKey(organization.Id);

        // Create users with SDK keys and generate their org keys
        var ownerEmail = mangleId.MangleEmail(request.OwnerEmail);
        var ownerUser = UserSeeder.CreateUserWithSdkKeys(ownerEmail, passwordHasher);
        var ownerOrgKey = RustSdkService.GenerateUserOrganizationKey(ownerUser.PublicKey!, orgKeys.Key);

        var adminEmail = mangleId.MangleEmail(request.AdminEmail);
        var adminUser = UserSeeder.CreateUserWithSdkKeys(adminEmail, passwordHasher);
        var adminOrgKey = RustSdkService.GenerateUserOrganizationKey(adminUser.PublicKey!, orgKeys.Key);

        var userEmail = mangleId.MangleEmail(request.UserEmail);
        var regularUser = UserSeeder.CreateUserWithSdkKeys(userEmail, passwordHasher);
        var regularOrgKey = RustSdkService.GenerateUserOrganizationKey(regularUser.PublicKey!, orgKeys.Key);

        var customEmail = mangleId.MangleEmail(request.CustomUserEmail);
        var customUser = UserSeeder.CreateUserWithSdkKeys(customEmail, passwordHasher);
        var customOrgKey = RustSdkService.GenerateUserOrganizationKey(customUser.PublicKey!, orgKeys.Key);

        var custom2Email = mangleId.MangleEmail(request.CustomUser2Email);
        var custom2User = UserSeeder.CreateUserWithSdkKeys(custom2Email, passwordHasher);
        var custom2OrgKey = RustSdkService.GenerateUserOrganizationKey(custom2User.PublicKey!, orgKeys.Key);

        // Save organization and API key first
        await organizationRepository.CreateAsync(organization);
        await organizationApiKeyRepository.CreateAsync(apiKey);

        // Save users to database (CreateAsync assigns new IDs via SetNewId)
        await userRepository.CreateAsync(ownerUser);
        await userRepository.CreateAsync(adminUser);
        await userRepository.CreateAsync(regularUser);
        await userRepository.CreateAsync(customUser);
        await userRepository.CreateAsync(custom2User);

        // Create organization users AFTER users are saved (to use actual DB IDs)
        var ownerOrgUser = organization.CreateOrganizationUserWithKey(
            ownerUser,
            OrganizationUserType.Owner,
            OrganizationUserStatusType.Confirmed,
            ownerOrgKey);

        var adminOrgUser = organization.CreateOrganizationUserWithKey(
            adminUser,
            OrganizationUserType.Admin,
            OrganizationUserStatusType.Confirmed,
            adminOrgKey);

        var regularOrgUser = organization.CreateOrganizationUserWithKey(
            regularUser,
            OrganizationUserType.User,
            OrganizationUserStatusType.Confirmed,
            regularOrgKey);

        var customOrgUser = organization.CreateOrganizationUserWithKey(
            customUser,
            OrganizationUserType.Custom,
            OrganizationUserStatusType.Confirmed,
            customOrgKey);

        var custom2OrgUser = organization.CreateOrganizationUserWithKey(
            custom2User,
            OrganizationUserType.Custom,
            OrganizationUserStatusType.Confirmed,
            custom2OrgKey);

        // Save organization users
        await organizationUserRepository.CreateAsync(ownerOrgUser);
        await organizationUserRepository.CreateAsync(adminOrgUser);
        await organizationUserRepository.CreateAsync(regularOrgUser);
        await organizationUserRepository.CreateAsync(customOrgUser);
        await organizationUserRepository.CreateAsync(custom2OrgUser);

        var mangleMap = new Dictionary<string, string?>
        {
            { request.OwnerEmail, ownerEmail },
            { request.AdminEmail, adminEmail },
            { request.UserEmail, userEmail },
            { request.CustomUserEmail, customEmail },
            { request.CustomUser2Email, custom2Email }
        };

        return new SceneResult<EnterpriseOrganizationSceneResult>(
            result: new EnterpriseOrganizationSceneResult
            {
                OrganizationId = organization.Id,
                OwnerEmail = ownerEmail,
                OwnerClientSecret = apiKey.ApiKey,
                AdminEmail = adminEmail,
                UserEmail = userEmail,
                CustomUserEmail = customEmail,
                CustomUser2Email = custom2Email
            },
            mangleMap: mangleMap);
    }
}
