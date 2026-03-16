using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Scenes;

public struct SingleUserSceneResult
{
    public Guid UserId { get; init; }
    public string Kdf { get; init; }
    public int KdfIterations { get; init; }
    public string Key { get; init; }
    public string DecryptedKeyB64 { get; init; }
    public string PublicKey { get; init; }
    public string PrivateKey { get; init; }
    public string ApiKey { get; init; }
}

/// <summary>
/// Creates a single user using the provided account details.
/// </summary>
public class SingleUserScene(
    IPasswordHasher<User> passwordHasher,
    IUserRepository userRepository,
    IManglerService manglerService) : IScene<SingleUserScene.Request, SingleUserSceneResult>
{
    public class Request
    {
        [Required]
        public required string Email { get; set; }
        [Required]
        public required string Password { get; set; }
        public bool EmailVerified { get; set; } = false;
        public bool Premium { get; set; } = false;
    }

    public async Task<SceneResult<SingleUserSceneResult>> SeedAsync(Request request)
    {
        // Pass service to factory - factory will call Mangle()
        var (user, keys) = UserSeeder.Create(
            request.Email,
            passwordHasher,
            manglerService,
            emailVerified: request.EmailVerified,
            premium: request.Premium,
            password: request.Password);

        await userRepository.CreateAsync(user);

        return new SceneResult<SingleUserSceneResult>(
            result: new SingleUserSceneResult
            {
                UserId = user.Id,
                Kdf = user.Kdf.ToString(),
                KdfIterations = user.KdfIterations,
                Key = user.Key!,
                PublicKey = user.PublicKey!,
                PrivateKey = user.PrivateKey!,
                ApiKey = user.ApiKey!,
                DecryptedKeyB64 = keys.Key
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
