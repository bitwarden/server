using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Scenes;

public struct SingleUserNoMasterPasswordSceneResult
{
    public Guid UserId { get; init; }
    public string DecryptedKeyB64 { get; init; }
    public string PublicKey { get; init; }
    public string PrivateKey { get; init; }
    public string ApiKey { get; init; }
}

public class SingleUserNoMasterPasswordScene(
    IPasswordHasher<User> passwordHasher,
    IUserRepository userRepository,
    IManglerService manglerService) : IScene<SingleUserNoMasterPasswordScene.Request, SingleUserNoMasterPasswordSceneResult>
{
    public class Request
    {
        [Required]
        public required string Email { get; set; }
        public bool EmailVerified { get; set; } = false;
    }

    public async Task<SceneResult<SingleUserNoMasterPasswordSceneResult>> SeedAsync(Request request)
    {
        var (user, keys) = UserSeeder.Create(
            new UserSeed
            {
                Email = request.Email,
                EmailVerified = request.EmailVerified,
                NoMasterPassword = true
            },
            passwordHasher,
            manglerService);

        await userRepository.CreateAsync(user);

        return new SceneResult<SingleUserNoMasterPasswordSceneResult>(
            result: new SingleUserNoMasterPasswordSceneResult
            {
                UserId = user.Id,
                PublicKey = user.PublicKey!,
                PrivateKey = user.PrivateKey!,
                ApiKey = user.ApiKey,
                DecryptedKeyB64 = keys.Key
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
