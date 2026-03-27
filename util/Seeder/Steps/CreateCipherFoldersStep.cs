using Bit.Seeder.Factories.Vault;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Resolves preset folder assignments, setting <c>Cipher.Folders</c> JSON for each
/// <c>(cipher, user, folder)</c> tuple declared in the preset.
/// </summary>
internal sealed class CreateCipherFoldersStep(List<SeedFolderAssignment> assignments) : IStep
{
    public void Execute(SeederContext context)
    {
        var cipherNames = context.Registry.FixtureCipherNameToId;
        var emailToUserId = context.Registry.UserEmailPrefixToUserId;
        var namedFolders = context.Registry.UserNamedFolders;

        // Phase 1: Validate all references before any mutations
        foreach (var a in assignments)
        {
            if (!cipherNames.ContainsKey(a.Cipher))
            {
                var available = string.Join(", ", cipherNames.Keys.OrderBy(k => k));
                throw new InvalidOperationException(
                    $"Folder assignment references unknown cipher '{a.Cipher}'. Available ciphers: {available}");
            }

            if (!emailToUserId.ContainsKey(a.User))
            {
                var available = string.Join(", ", emailToUserId.Keys.OrderBy(k => k));
                throw new InvalidOperationException(
                    $"Folder assignment references unknown user '{a.User}'. Available users: {available}");
            }

            if (!namedFolders.TryGetValue(a.User, out var userFolders) || !userFolders.ContainsKey(a.Folder))
            {
                var available = userFolders != null
                    ? string.Join(", ", userFolders.Keys.OrderBy(k => k))
                    : "(none)";
                throw new InvalidOperationException(
                    $"Folder assignment references unknown folder '{a.Folder}' for user '{a.User}'. " +
                    $"Available folders for '{a.User}': {available}");
            }
        }

        // Phase 2: Accumulate (cipherId → {userId → folderId}) mappings
        var cipherFolderMap = new Dictionary<Guid, Dictionary<Guid, Guid>>();
        var seen = new HashSet<(Guid CipherId, Guid UserId)>();

        foreach (var a in assignments)
        {
            var cipherId = cipherNames[a.Cipher];
            var userId = emailToUserId[a.User];
            var folderId = namedFolders[a.User][a.Folder];

            if (!seen.Add((cipherId, userId)))
            {
                throw new InvalidOperationException(
                    $"Duplicate folder assignment: cipher '{a.Cipher}' + user '{a.User}' appears more than once.");
            }

            if (!cipherFolderMap.TryGetValue(cipherId, out var userFolderMap))
            {
                userFolderMap = new Dictionary<Guid, Guid>();
                cipherFolderMap[cipherId] = userFolderMap;
            }

            userFolderMap[userId] = folderId;
        }

        // Phase 3: Set Cipher.Folders JSON
        var cipherLookup = context.Ciphers.ToDictionary(c => c.Id);

        foreach (var (cipherId, userFolderMap) in cipherFolderMap)
        {
            cipherLookup[cipherId].Folders = CipherComposer.BuildFoldersJson(userFolderMap);
        }
    }
}
