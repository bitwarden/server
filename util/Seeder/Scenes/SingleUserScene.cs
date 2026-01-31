using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Seeder.Factories;

namespace Bit.Seeder.Scenes;

public struct SingleUserSceneResult
{
    public Guid UserId { get; init; }
    public string Kdf { get; init; }
    public int KdfIterations { get; init; }
    public string Key { get; init; }
    public string PublicKey { get; init; }
    public string PrivateKey { get; init; }
    public string ApiKey { get; init; }

}

/// <summary>
/// Creates a single user using the provided account details.
/// </summary>
public class SingleUserScene(UserSeeder userSeeder, IUserRepository userRepository) : IScene<SingleUserScene.Request, SingleUserSceneResult>
{
    public class Request
    {
        [Required]
        public required string Email { get; set; }
        public bool EmailVerified { get; set; } = false;
        public bool Premium { get; set; } = false;
    }

    public async Task<SceneResult<SingleUserSceneResult>> SeedAsync(Request request)
    {
        var user = userSeeder.CreateUser(request.Email, request.EmailVerified, request.Premium);

        await userRepository.CreateAsync(user);

        return new SceneResult<SingleUserSceneResult>(result: new SingleUserSceneResult
        {
            UserId = user.Id,
            Kdf = user.Kdf.ToString(),
            KdfIterations = user.KdfIterations,
            Key = user.Key!,
            PublicKey = user.PublicKey!,
            PrivateKey = user.PrivateKey!,
            ApiKey = user.ApiKey!,
        }, mangleMap: userSeeder.GetMangleMap(user, new UserData
        {
            Email = request.Email,
        }));
    }
}
