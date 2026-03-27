using Bit.Seeder.Factories.Vault;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates folders with explicit names for the first user, registering them as named folders for preset-driven assignments.
/// </summary>
internal sealed class CreateNamedFoldersStep(List<string> folderNames) : IStep
{
    public void Execute(SeederContext context)
    {
        var userDigest = context.Registry.UserDigests[0];
        var emailPrefix = context.Registry.UserEmailPrefixToUserId
            .First(kvp => kvp.Value == userDigest.UserId).Key;

        var namedFolders = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var folderIds = new List<Guid>(folderNames.Count);

        foreach (var folderName in folderNames)
        {
            if (namedFolders.ContainsKey(folderName))
            {
                throw new InvalidOperationException(
                    $"Duplicate folder name '{folderName}' in folderNames list.");
            }

            var folder = FolderSeeder.Create(userDigest.UserId, userDigest.SymmetricKey, folderName);
            context.Folders.Add(folder);
            namedFolders[folderName] = folder.Id;
            folderIds.Add(folder.Id);
        }

        context.Registry.UserNamedFolders[emailPrefix] = namedFolders;
        context.Registry.UserFolderIds[userDigest.UserId] = folderIds;
    }
}
