using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Repositories;
using Bit.Seeder.Extensions;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public struct SsoUserLinkSceneResult
{
    public long SsoUserId { get; init; }
}

public class SsoUserLinkScene(
    IOrganizationUserRepository organizationUserRepository,
    ISsoUserRepository ssoUserRepository,
    IUserRepository userRepository,
    IManglerService manglerService) : IScene<SsoUserLinkScene.Request, SsoUserLinkSceneResult>
{
    public class Request
    {
        [Required]
        public required Guid UserId { get; set; }
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        [MaxLength(300)]
        public required string ExternalId { get; set; }
    }

    public async Task<SceneResult<SsoUserLinkSceneResult>> SeedAsync(Request request)
    {
        if (string.IsNullOrWhiteSpace(request.ExternalId) || request.ExternalId.Length > 300)
        {
            throw new InvalidOperationException("ExternalId must be 1 to 300 characters.");
        }

        await organizationUserRepository.ThrowIfUserNotConfirmedOrRevokedMemberOfOrganizationAsync(
            request.UserId, request.OrganizationId);

        if (await ssoUserRepository.GetByUserIdOrganizationIdAsync(request.OrganizationId, request.UserId) is not null)
        {
            throw new InvalidOperationException(
                $"User {request.UserId} is already linked in organization {request.OrganizationId}.");
        }

        if (await userRepository.GetBySsoUserAsync(request.ExternalId, request.OrganizationId) is not null)
        {
            throw new InvalidOperationException(
                $"ExternalId is already linked to a user in organization {request.OrganizationId}.");
        }

        var ssoUser = new SsoUser
        {
            UserId = request.UserId,
            OrganizationId = request.OrganizationId,
            ExternalId = request.ExternalId
        };
        await ssoUserRepository.CreateAsync(ssoUser);

        return new SceneResult<SsoUserLinkSceneResult>(
            result: new SsoUserLinkSceneResult
            {
                SsoUserId = ssoUser.Id
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
