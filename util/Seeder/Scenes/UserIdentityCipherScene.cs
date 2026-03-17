using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class UserIdentityCipherScene(IUserRepository userRepository, ICipherRepository cipherRepository, IManglerService manglerService) : IScene<UserIdentityCipherScene.Request, UserIdentityCipherScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid UserId { get; set; }
        [Required]
        public required string UserKeyB64 { get; set; }
        [Required]
        public required string Name { get; set; }
        public string? Title { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
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

        var identity = new IdentityViewDto
        {
            Title = request.Title,
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            LastName = request.LastName
        };
        var cipher = IdentityCipherSeeder.Create(request.UserKeyB64, request.Name, userId: request.UserId, identity: identity, notes: request.Notes);

        await cipherRepository.CreateAsync(cipher);

        return new SceneResult<Result>(
            result: new Result
            {
                CipherId = cipher.Id
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
