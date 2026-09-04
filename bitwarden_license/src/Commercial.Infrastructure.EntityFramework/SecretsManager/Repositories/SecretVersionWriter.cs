using AutoMapper;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using Microsoft.EntityFrameworkCore;

namespace Bit.Commercial.Infrastructure.EntityFramework.SecretsManager.Repositories;

/// <summary>
/// Shared version-write logic for the repositories that persist secret versions.
/// </summary>
/// <remarks>
/// This takes an existing <see cref="DatabaseContext"/> rather than opening its own scope so the
/// version write can join the caller's transaction. Writing a secret and its version has to be
/// atomic: if the version were written separately and failed, the caller would receive an error for
/// a secret that was already committed, and a retry would duplicate it.
/// The caller owns calling SaveChanges and committing.
/// </remarks>
internal static class SecretVersionWriter
{
    private const int MaxVersionsToKeep = 10;

    /// <summary>
    /// Trims the secret's history so that adding <paramref name="secretVersion"/> leaves at most
    /// <see cref="MaxVersionsToKeep"/> versions, then adds it to the change tracker.
    /// </summary>
    public static async Task AddWithPruningAsync(
        DatabaseContext dbContext,
        IMapper mapper,
        Core.SecretsManager.Entities.SecretVersion secretVersion)
    {
        // Leave room for the version about to be added.
        var versionsToKeepIds = await dbContext.SecretVersion
            .Where(sv => sv.SecretId == secretVersion.SecretId)
            .OrderByDescending(sv => sv.VersionDate)
            .ThenByDescending(sv => sv.Id)
            .Take(MaxVersionsToKeep - 1)
            .Select(sv => sv.Id)
            .ToListAsync();

        if (versionsToKeepIds.Count > 0)
        {
            await dbContext.SecretVersion
                .Where(sv => sv.SecretId == secretVersion.SecretId && !versionsToKeepIds.Contains(sv.Id))
                .ExecuteDeleteAsync();
        }

        secretVersion.SetNewId();
        await dbContext.AddAsync(mapper.Map<SecretVersion>(secretVersion));
    }
}
