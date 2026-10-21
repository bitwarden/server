using Bit.Core.Vault.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bit.Infrastructure.EntityFramework.Vault.Repositories;

public static class CipherDatabaseContextExtensions
{
    /// <summary>
    /// Replaces a cipher's data when its revision date is within a second of <paramref name="lastKnownRevisionDate"/>,
    /// the EF counterpart of <c>Cipher_UpdateData</c>. Runs on the caller's context and transaction: it never opens a
    /// transaction or saves, and the caller's save persists the account revision bumps.
    /// </summary>
    public static async Task<CipherDataUpdateResult> CipherUpdateDataAsync(this DatabaseContext context,
        Guid cipherId, Guid organizationId, string data, DateTime lastKnownRevisionDate, DateTime revisionDate)
    {
        // The version check sits in the UPDATE rather than a read before it. Bounds are parameters, so every provider
        // can translate them, and they clamp so an out-of-range date reads as a mismatch instead of throwing. Same
        // 1-second tolerance as CipherService.
        var lowerBound = lastKnownRevisionDate > DateTime.MinValue.AddSeconds(1)
            ? lastKnownRevisionDate.AddSeconds(-1)
            : DateTime.MinValue;
        var upperBound = lastKnownRevisionDate < DateTime.MaxValue.AddSeconds(-1)
            ? lastKnownRevisionDate.AddSeconds(1)
            : DateTime.MaxValue;

        var updated = await context.Ciphers
            .Where(c => c.Id == cipherId && c.RevisionDate >= lowerBound && c.RevisionDate <= upperBound)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Data, data)
                .SetProperty(c => c.RevisionDate, revisionDate));

        if (updated == 0)
        {
            return await context.Ciphers.AnyAsync(c => c.Id == cipherId)
                ? CipherDataUpdateResult.RevisionMismatch
                : CipherDataUpdateResult.NotFound;
        }

        await context.UserBumpAccountRevisionDateByCipherIdAsync(cipherId, organizationId);
        return CipherDataUpdateResult.Updated;
    }
}
