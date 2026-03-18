using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class UserSshKeyCipherScene(IUserRepository userRepository, ICipherRepository cipherRepository, IManglerService manglerService) : IScene<UserSshKeyCipherScene.Request, UserSshKeyCipherScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid UserId { get; set; }
        [Required]
        public required string UserKeyB64 { get; set; }
        [Required]
        public required string Name { get; set; }
        public string? PrivateKey { get; set; }
        public string? PublicKey { get; set; }
        public string? Fingerprint { get; set; }
        public bool Reprompt { get; set; }
        public string? Notes { get; set; }
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

        var sshKey = new SshKeyViewDto
        {
            PrivateKey = request.PrivateKey,
            PublicKey = request.PublicKey,
            Fingerprint = request.Fingerprint
        };
        var cipher = SshKeyCipherSeeder.Create(request.UserKeyB64, request.Name, userId: request.UserId, sshKey: sshKey, notes: request.Notes, reprompt: request.Reprompt);

        await cipherRepository.CreateAsync(cipher);

        return new SceneResult<Result>(
            result: new Result
            {
                CipherId = cipher.Id
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
