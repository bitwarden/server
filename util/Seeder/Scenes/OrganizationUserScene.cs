using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public struct OrganizationUserSceneResult
{
    public Guid OrganizationUserId { get; init; }
}

/// <summary>
/// Links an existing user to an existing organization as an OrganizationUser with the requested
/// role and status. For Confirmed/Revoked statuses the organization's symmetric key is encrypted
/// to the user's public key; Invited/Accepted statuses carry no key.
/// </summary>
public class OrganizationUserScene(
    IUserRepository userRepository,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IManglerService manglerService) : IScene<OrganizationUserScene.Request, OrganizationUserSceneResult>
{
    public class Request
    {
        [Required]
        public required Guid UserId { get; set; }
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        public required string OrganizationKeyB64 { get; set; }
        [Required]
        public required OrganizationUserType OrganizationUserType { get; set; }
        [Required]
        public required OrganizationUserStatusType OrganizationUserStatusType { get; set; }
        public Permissions? Permissions { get; set; }
        public bool AccessSecretsManager { get; set; }
    }

    public async Task<SceneResult<OrganizationUserSceneResult>> SeedAsync(Request request)
    {
        var user = await userRepository.GetByIdAsync(request.UserId);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {request.UserId} not found.");
        }

        var requiresKey = request.OrganizationUserStatusType is OrganizationUserStatusType.Confirmed
            or OrganizationUserStatusType.Revoked;

        if (requiresKey && string.IsNullOrEmpty(user.PublicKey))
        {
            throw new InvalidOperationException(
                $"User {request.UserId} has no public key; cannot encrypt the organization key for the member.");
        }

        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            throw new InvalidOperationException($"Organization {request.OrganizationId} not found.");
        }

        var encryptedOrgKey = requiresKey
            ? RustSdkService.GenerateUserOrganizationKey(user.PublicKey!, request.OrganizationKeyB64)
            : null;

        var organizationUser = organization.CreateOrganizationUserWithKey(
            user,
            request.OrganizationUserType,
            request.OrganizationUserStatusType,
            encryptedOrgKey);

        if (request.OrganizationUserType == OrganizationUserType.Custom)
        {
            organizationUser.SetPermissions(request.Permissions ?? new Permissions());
        }

        organizationUser.AccessSecretsManager = request.AccessSecretsManager;

        await organizationUserRepository.CreateAsync(organizationUser);

        return new SceneResult<OrganizationUserSceneResult>(
            result: new OrganizationUserSceneResult
            {
                OrganizationUserId = organizationUser.Id
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
