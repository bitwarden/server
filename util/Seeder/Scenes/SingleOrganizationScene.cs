using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public struct SingleOrganizationSceneResult
{
    public Guid OrganizationId { get; init; }
    public Guid OrganizationUserId { get; init; }
    public string ApiKey { get; init; }
    public string OrganizationKeyB64 { get; init; }
}

/// <summary>
/// Seeds an organization on the requested plan and links an existing user to it as a confirmed owner.
/// </summary>
public class SingleOrganizationScene(
    IUserRepository userRepository,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationApiKeyRepository organizationApiKeyRepository,
    IManglerService manglerService) : IScene<SingleOrganizationScene.Request, SingleOrganizationSceneResult>
{
    public class Request
    {
        [Required]
        public required Guid OwnerUserId { get; set; }
        [Required]
        public required PlanType PlanType { get; set; }
        [Required]
        public required string Name { get; set; }
        [Required]
        public required string Domain { get; set; }
        [Required]
        public required int Seats { get; set; }
    }

    public async Task<SceneResult<SingleOrganizationSceneResult>> SeedAsync(Request request)
    {
        var user = await userRepository.GetByIdAsync(request.OwnerUserId)
            ?? throw new InvalidOperationException($"User {request.OwnerUserId} not found.");

        if (string.IsNullOrEmpty(user.PublicKey))
        {
            throw new InvalidOperationException(
                $"User {request.OwnerUserId} has no public key; cannot encrypt the organization key for the owner.");
        }

        var orgKeys = RustSdkService.GenerateOrganizationKeys();

        var organization = OrganizationSeeder.Create(
            request.Name,
            request.Domain,
            request.Seats,
            manglerService,
            orgKeys.PublicKey,
            orgKeys.PrivateKey,
            request.PlanType);

        await organizationRepository.CreateAsync(organization);

        var ownerOrgKey = RustSdkService.GenerateUserOrganizationKey(user.PublicKey, orgKeys.Key);
        var organizationUser = organization.CreateOrganizationUserWithKey(
            user,
            OrganizationUserType.Owner,
            OrganizationUserStatusType.Confirmed,
            ownerOrgKey);

        await organizationUserRepository.CreateAsync(organizationUser);

        var apiKey = new OrganizationApiKey
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            Type = OrganizationApiKeyType.Default,
            ApiKey = CoreHelpers.SecureRandomString(30),
            RevisionDate = DateTime.UtcNow,
        };

        await organizationApiKeyRepository.CreateAsync(apiKey);

        return new SceneResult<SingleOrganizationSceneResult>(
            result: new SingleOrganizationSceneResult
            {
                OrganizationId = organization.Id,
                OrganizationUserId = organizationUser.Id,
                ApiKey = apiKey.ApiKey,
                OrganizationKeyB64 = orgKeys.Key
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
