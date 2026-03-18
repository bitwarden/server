using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class UserLoginCipherScene(IUserRepository userRepository, ICipherRepository cipherRepository, IManglerService manglerService) : IScene<UserLoginCipherScene.Request, UserLoginCipherScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid UserId { get; set; }
        [Required]
        public required string UserKeyB64 { get; set; }
        [Required]
        public required string Name { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Uri { get; set; }
        public string? Notes { get; set; }
        public bool Reprompt { get; set; }
        public bool Deleted { get; set; }
        public bool Favorite { get; set; }
        public IEnumerable<(string name, string value, int type)>? Fields { get; set; }
    }

    public class Result
    {
        public required Guid CipherId { get; set; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        var user = await userRepository.GetByIdAsync(request.UserId);
        if (user == null)
        {
            throw new Exception($"User with ID {request.UserId} not found.");
        }

        var cipher = LoginCipherSeeder.Create(request.UserKeyB64, request.Name, userId: request.UserId, username: request.Username, password: request.Password, uri: request.Uri, notes: request.Notes, fields: request.Fields, reprompt: request.Reprompt, deleted: request.Deleted, favorite: request.Favorite);

        await cipherRepository.CreateAsync(cipher);

        return new SceneResult<Result>(
            result: new Result
            {
                CipherId = cipher.Id
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
