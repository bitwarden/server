using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class UserFolderScene(IUserRepository userRepository, IFolderRepository folderRepository, IManglerService manglerService) : IScene<UserFolderScene.Request, UserFolderScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid UserId { get; set; }
        [Required]
        public required string UserKeyB64 { get; set; }
        [Required]
        public required string FolderName { get; set; }
    }

    public class Result
    {
        public Guid FolderId { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        var user = await userRepository.GetByIdAsync(request.UserId);
        if (user == null)
        {
            throw new Exception($"User with ID {request.UserId} not found.");
        }

        var folder = FolderSeeder.Create(request.UserId, request.UserKeyB64, request.FolderName);
        await folderRepository.CreateAsync(folder);

        return new SceneResult<Result>(
            result: new Result
            {
                FolderId = folder.Id,
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
