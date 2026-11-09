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
        await organizationUserRepository.ThrowIfUserNotConfirmedOrRevokedMemberOfOrganizationAsync(
            request.UserId, request.OrganizationId);

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
