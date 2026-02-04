using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Scenes;

public struct FreeOrganizationSceneResult
{
    public Guid OrganizationId { get; init; }
    public string OwnerEmail { get; init; }
    public string UserEmail { get; init; }
}

/// <summary>
/// Creates a Free-tier organization with an owner and a member user.
/// </summary>
public class FreeOrganizationScene(
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IUserRepository userRepository,
    IPasswordHasher<Core.Entities.User> passwordHasher,
    MangleId mangleId) : IScene<FreeOrganizationScene.Request, FreeOrganizationSceneResult>
{
    public class Request
    {
        public string OrganizationName { get; set; } = "Free Test Org";

        [Required]
        public required string OwnerEmail { get; set; }

        [Required]
        public required string UserEmail { get; set; }
    }

    public async Task<SceneResult<FreeOrganizationSceneResult>> SeedAsync(Request request)
    {
        var domain = MangleId.ExtractDomain(request.OwnerEmail);

        // Generate organization keys
        var orgKeys = RustSdkService.GenerateOrganizationKeys();

        // Create Free organization
        var organization = OrganizationSeeder.CreateFree(
            request.OrganizationName,
            domain,
            orgKeys.PublicKey,
            orgKeys.PrivateKey);

        // Create users with SDK-generated keys and generate their org keys
        var ownerEmail = mangleId.MangleEmail(request.OwnerEmail);
        var ownerUser = UserSeeder.CreateUserWithSdkKeys(ownerEmail, passwordHasher);
        var ownerOrgKey = RustSdkService.GenerateUserOrganizationKey(ownerUser.PublicKey!, orgKeys.Key);

        var memberEmail = mangleId.MangleEmail(request.UserEmail);
        var memberUser = UserSeeder.CreateUserWithSdkKeys(memberEmail, passwordHasher);
        var memberOrgKey = RustSdkService.GenerateUserOrganizationKey(memberUser.PublicKey!, orgKeys.Key);

        // Save organization and users first
        await organizationRepository.CreateAsync(organization);
        await userRepository.CreateAsync(ownerUser);
        await userRepository.CreateAsync(memberUser);

        // Create organization users after users are saved
        var ownerOrgUser = organization.CreateOrganizationUserWithKey(
            ownerUser,
            OrganizationUserType.Owner,
            OrganizationUserStatusType.Confirmed,
            ownerOrgKey);

        var memberOrgUser = organization.CreateOrganizationUserWithKey(
            memberUser,
            OrganizationUserType.User,
            OrganizationUserStatusType.Confirmed,
            memberOrgKey);

        // Save organization users
        await organizationUserRepository.CreateAsync(ownerOrgUser);
        await organizationUserRepository.CreateAsync(memberOrgUser);

        var mangleMap = new Dictionary<string, string?>
        {
            { request.OwnerEmail, ownerEmail },
            { request.UserEmail, memberEmail }
        };

        return new SceneResult<FreeOrganizationSceneResult>(
            result: new FreeOrganizationSceneResult
            {
                OrganizationId = organization.Id,
                OwnerEmail = ownerEmail,
                UserEmail = memberEmail
            },
            mangleMap: mangleMap);
    }
}
