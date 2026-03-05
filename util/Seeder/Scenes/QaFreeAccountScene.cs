using System.ComponentModel.DataAnnotations;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class QaFreeAccountScene(
    IFolderRepository folderRepository,
    ICipherRepository cipherRepository,
    ISeedReader seedReader,
    IManglerService manglerService) : IScene<QaFreeAccountScene.Request, QaFreeAccountScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid UserId { get; set; }
        [Required]
        public required string UserKeyB64 { get; set; }
    }

    public class Result
    {
        public List<Guid> FolderIds { get; init; } = new();
        public List<Guid> CipherIds { get; init; } = new();
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        // Load cipher fixture
        var seedFile = seedReader.Read<SeedFile>("ciphers.qa-free-account");

        // Create folders from FreeAccount.json
        // FIXME: this should be a seed file, too
        var folderNames = new[]
        {
            "Folder-19597",
            "Folder-19598",
            "Folder-19553",
            "Folder-19617",
            "Folder-19928",
            "Folder-1867255"
        };

        var folders = new List<Folder>();
        var folderNameToId = new Dictionary<string, Guid>();

        foreach (var folderName in folderNames)
        {
            var folder = FolderSeeder.Create(request.UserId, request.UserKeyB64, folderName);
            folders.Add(folder);
            folderNameToId[folderName] = folder.Id;
            await folderRepository.CreateAsync(folder);
        }

        // Create personal ciphers from fixture
        var ciphers = new List<Cipher>();

        foreach (var item in seedFile.Items)
        {
            var cipher = item.Type switch
            {
                "login" => LoginCipherSeeder.CreateFromSeed(request.UserKeyB64, item, userId: request.UserId),
                "card" => CardCipherSeeder.CreateFromSeed(request.UserKeyB64, item, userId: request.UserId),
                "identity" => IdentityCipherSeeder.CreateFromSeed(request.UserKeyB64, item, userId: request.UserId),
                "secureNote" => SecureNoteCipherSeeder.CreateFromSeed(request.UserKeyB64, item, userId: request.UserId),
                "sshKey" => SshKeyCipherSeeder.CreateFromSeed(request.UserKeyB64, item, userId: request.UserId),
                _ => throw new InvalidOperationException($"Unknown cipher type: {item.Type}")
            };

            // Assign Cipher-19617 to Folder-19617
            // TODO: This should be a capability added to seed files
            if (item.Name == "Cipher-19617" && folderNameToId.TryGetValue("Folder-19617", out var folderId))
            {
                cipher.Folders = $"{{\"{request.UserId.ToString().ToUpperInvariant()}\":\"{folderId.ToString().ToUpperInvariant()}\"}}";
            }

            // Mark specific ciphers as favorites
            // TODO: This should be a capability added to seed files
            var favoriteCipherNames = new[] { "Cipher-19617", "Cipher-19546", "Cipher-19848" };
            if (favoriteCipherNames.Contains(item.Name))
            {
                cipher.Favorites = $"{{\"{request.UserId.ToString().ToUpperInvariant()}\":true}}";
            }

            ciphers.Add(cipher);
            await cipherRepository.CreateAsync(cipher);
        }

        return new SceneResult<Result>(
            result: new Result
            {
                FolderIds = folders.Select(f => f.Id).ToList(),
                CipherIds = ciphers.Select(c => c.Id).ToList()
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
