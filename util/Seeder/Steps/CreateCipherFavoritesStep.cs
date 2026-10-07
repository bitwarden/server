using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Resolves preset favorite assignments, setting <c>Cipher.Favorites</c> JSON for each
/// <c>(cipher, user)</c> tuple declared in the preset.
/// </summary>
internal sealed class CreateCipherFavoritesStep(List<SeedFavoriteAssignment> assignments) : IStep
{
    public void Execute(SeederContext context)
    {
        var cipherNames = context.Registry.FixtureCipherNameToId;
        var emailToUserId = context.Registry.UserEmailPrefixToUserId;

        // Phase 1: Validate all references before any mutations
        foreach (var a in assignments)
        {
            if (!cipherNames.ContainsKey(a.Cipher))
            {
                var available = string.Join(", ", cipherNames.Keys.OrderBy(k => k));
                throw new InvalidOperationException(
                    $"Favorite assignment references unknown cipher '{a.Cipher}'. Available ciphers: {available}");
            }

            if (!emailToUserId.ContainsKey(a.User))
            {
                var available = string.Join(", ", emailToUserId.Keys.OrderBy(k => k));
                throw new InvalidOperationException(
                    $"Favorite assignment references unknown user '{a.User}'. Available users: {available}");
            }
        }

        // Phase 2: Accumulate (cipherId → [userId]) mappings
        var cipherFavoriteMap = new Dictionary<Guid, List<Guid>>();
        var seen = new HashSet<(Guid CipherId, Guid UserId)>();

        foreach (var a in assignments)
        {
            var cipherId = cipherNames[a.Cipher];
            var userId = emailToUserId[a.User];

            if (!seen.Add((cipherId, userId)))
            {
                throw new InvalidOperationException(
                    $"Duplicate favorite assignment: cipher '{a.Cipher}' + user '{a.User}' appears more than once.");
            }

            if (!cipherFavoriteMap.TryGetValue(cipherId, out var userIds))
            {
                userIds = [];
                cipherFavoriteMap[cipherId] = userIds;
            }

            userIds.Add(userId);
        }

        // Phase 3: Set Cipher.Favorites JSON
        var cipherLookup = context.Ciphers.ToDictionary(c => c.Id);

        foreach (var (cipherId, userIds) in cipherFavoriteMap)
        {
            cipherLookup[cipherId].Favorites = CipherComposer.BuildFavoritesJson(userIds);
        }
    }
}
