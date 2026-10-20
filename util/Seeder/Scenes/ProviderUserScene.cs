using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public struct ProviderUserSceneResult
{
    public Guid ProviderUserId { get; init; }
}

/// <summary>
/// Links an existing user to an existing provider as a ProviderUser with the requested role and status.
/// Confirmed members receive the provider key encrypted to their public key; Invited and Accepted members
/// carry no key.
/// </summary>
public class ProviderUserScene(
    IUserRepository userRepository,
    IProviderRepository providerRepository,
    IProviderUserRepository providerUserRepository,
    IManglerService manglerService) : IScene<ProviderUserScene.Request, ProviderUserSceneResult>
{
    public class Request
    {
        [Required]
        public required Guid UserId { get; set; }
        [Required]
        public required Guid ProviderId { get; set; }
        [Required]
        public required string ProviderKeyB64 { get; set; }
        [Required]
        public required ProviderUserType ProviderUserType { get; set; }
        [Required]
        public required ProviderUserStatusType ProviderUserStatusType { get; set; }
    }

    public async Task<SceneResult<ProviderUserSceneResult>> SeedAsync(Request request)
    {
        var user = await userRepository.GetByIdAsync(request.UserId);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {request.UserId} not found.");
        }

        var requiresKey = request.ProviderUserStatusType == ProviderUserStatusType.Confirmed;

        if (requiresKey && string.IsNullOrEmpty(user.PublicKey))
        {
            throw new InvalidOperationException(
                $"User {request.UserId} has no public key; cannot encrypt the provider key for the member.");
        }

        var provider = await providerRepository.GetByIdAsync(request.ProviderId);
        if (provider == null)
        {
            throw new InvalidOperationException($"Provider {request.ProviderId} not found.");
        }

        var encryptedProviderKey = requiresKey
            ? RustSdkService.GenerateUserOrganizationKey(user.PublicKey!, request.ProviderKeyB64)
            : null;

        var providerUser = ProviderUserSeeder.CreateProviderUser(
            provider,
            user,
            request.ProviderUserType,
            request.ProviderUserStatusType,
            encryptedProviderKey);

        await providerUserRepository.CreateAsync(providerUser);

        return new SceneResult<ProviderUserSceneResult>(
            result: new ProviderUserSceneResult
            {
                ProviderUserId = providerUser.Id
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
