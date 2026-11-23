using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Extensions;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class UserAuthRequestScene(
    IUserRepository userRepository,
    IOrganizationUserRepository organizationUserRepository,
    IAuthRequestRepository authRequestRepository,
    IManglerService manglerService) : IScene<UserAuthRequestScene.Request, UserAuthRequestScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid UserId { get; set; }
        [Required]
        public required AuthRequestType Type { get; set; }

        public Guid? OrganizationId { get; set; }

        public DeviceType RequestDeviceType { get; set; } = DeviceType.ChromeBrowser;
        [MaxLength(50)]
        public string? RequestDeviceIdentifier { get; set; }
        [MaxLength(50)]
        public string RequestIpAddress { get; set; } = "127.0.0.1";
        [MaxLength(200)]
        public string? RequestCountryName { get; set; } = "United States";
    }

    public class Result
    {
        public Guid AuthRequestId { get; init; }
        public required string AccessCode { get; init; }
        public required string PublicKey { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        var user = await userRepository.GetUserOrThrowAsync(request.UserId);

        if (request.Type == AuthRequestType.AdminApproval)
        {
            var organizationId = request.OrganizationId
                ?? throw new ArgumentException("OrganizationId is required for the AdminApproval type.");
            await organizationUserRepository.ThrowIfUserNotConfirmedOrRevokedMemberOfOrganizationAsync(
                user.Id, organizationId);
        }
        else if (request.OrganizationId.HasValue)
        {
            throw new ArgumentException($"OrganizationId is only valid for the AdminApproval type, not {request.Type}.");
        }

        var requestKeyPair = RustSdkService.GenerateOrganizationKeys();

        var authRequest = AuthRequestSeeder.CreatePending(
            user.Id,
            request.OrganizationId,
            request.Type,
            request.RequestDeviceType,
            request.RequestDeviceIdentifier ?? Guid.NewGuid().ToString(),
            request.RequestIpAddress,
            request.RequestCountryName,
            requestKeyPair.PublicKey);

        await authRequestRepository.CreateAsync(authRequest);

        return new SceneResult<Result>(
            result: new Result
            {
                AuthRequestId = authRequest.Id,
                AccessCode = authRequest.AccessCode,
                PublicKey = authRequest.PublicKey,
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
