using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Seeder.Factories;

namespace Bit.Seeder.Scenes;

public class SingleUserScene(UserSeeder userSeeder, IUserRepository userRepository) : IScene<SingleUserScene.Request>
{
    public class Request
    {
        [Required]
        public required string Email { get; set; }
        public bool EmailVerified { get; set; } = false;
        public bool Premium { get; set; } = false;
    }

    public async Task<SceneResult> SeedAsync(Request request)
    {
        var user = userSeeder.CreateUser(request.Email, request.EmailVerified, request.Premium);

        await userRepository.CreateAsync(user);

        return new SceneResult(mangleMap: userSeeder.GetMangleMap(user, new UserData
        {
            Email = request.Email,
            Id = user.Id,
            Key = user.Key,
            PublicKey = user.PublicKey,
            PrivateKey = user.PrivateKey,
            ApiKey = user.ApiKey,
            Kdf = user.Kdf,
            KdfIterations = user.KdfIterations,
        }));
    }
}
