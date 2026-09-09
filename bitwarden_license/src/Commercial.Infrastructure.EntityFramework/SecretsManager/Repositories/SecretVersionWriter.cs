using AutoMapper;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using Microsoft.EntityFrameworkCore;

namespace Bit.Commercial.Infrastructure.EntityFramework.SecretsManager.Repositories;

internal static class SecretVersionWriter
{
    private const int MaxVersionsToKeep = 10;

    /// <summary>
    /// Snapshots a secret's pre-update value when it has no version history yet, so the value being
    /// overwritten stays recoverable. Secrets written before versioning existed - or by any future
    /// create path that omits an initial version - would otherwise lose it silently on first edit.
    /// The editor is left unset because that earlier write was never attributed to anyone.
    /// </summary>
    /// <returns><c>true</c> when a snapshot was added to the change tracker.</returns>
    public static async Task<bool> TryBackfillPreviousVersionAsync(
        DatabaseContext dbContext,
        IMapper mapper,
        Guid secretId,
        string? previousValue,
        DateTime previousRevisionDate)
    {
        // SecretVersion.Value is non-nullable, and a null value has nothing worth recovering.
        if (previousValue == null)
        {
            return false;
        }

        if (await dbContext.SecretVersion.AnyAsync(sv => sv.SecretId == secretId))
        {
            return false;
        }

        var previousVersion = new Core.SecretsManager.Entities.SecretVersion
        {
            SecretId = secretId,
            Value = previousValue,
            VersionDate = previousRevisionDate
        };

        previousVersion.SetNewId();
        await dbContext.AddAsync(mapper.Map<SecretVersion>(previousVersion));
        return true;
    }

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
