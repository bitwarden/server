using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Api.AdminConsole.Partnerships.Idempotency;

/// <summary>
/// Stores partner responses by partnership and <c>Idempotency-Key</c> in the <see cref="CacheName"/> cache.
/// </summary>
/// <remarks>
/// A key is claimed with an in-flight entry that lives for <see cref="InFlightLifetime"/>, then replaced by the
/// response for <see cref="ResponseLifetime"/> or removed so the partner can retry. The claim is atomic within one
/// instance (FusionCache stampede protection) but not across instances, because FusionCache has no distributed
/// set-if-absent; two instances receiving the same key within the L2 round trip can both run the action.
/// </remarks>
public sealed class PartnershipIdempotencyStore(
    [FromKeyedServices(PartnershipIdempotencyStore.CacheName)] IFusionCache cache,
    IDataProtectionProvider dataProtectionProvider)
{
    public const string CacheName = "PartnershipIdempotency";
    public const string DataProtectorPurpose = "PartnershipIdempotencyResponse";
    public static readonly TimeSpan ResponseLifetime = TimeSpan.FromHours(24);

    /// <summary>
    /// How long a key stays claimed if its request never finishes, for example when the process dies mid-request.
    /// </summary>
    public static readonly TimeSpan InFlightLifetime = TimeSpan.FromMinutes(1);

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(DataProtectorPurpose);

    public async Task<PartnershipIdempotencyClaim> ClaimAsync(Guid partnershipId, string idempotencyKey,
        string requestHash)
    {
        var inFlightId = Guid.NewGuid();
        var entry = await cache.GetOrSetAsync(CacheKey(partnershipId, idempotencyKey),
            new PartnershipIdempotencyEntry { RequestHash = requestHash, InFlightId = inFlightId }, InFlightLifetime);

        if (entry.InFlightId == inFlightId)
        {
            return new PartnershipIdempotencyClaim(PartnershipIdempotencyOutcome.Acquired);
        }

        if (entry.InFlightId is not null)
        {
            return new PartnershipIdempotencyClaim(PartnershipIdempotencyOutcome.InProgress);
        }

        if (entry.RequestHash != requestHash)
        {
            return new PartnershipIdempotencyClaim(PartnershipIdempotencyOutcome.KeyReused);
        }

        return new PartnershipIdempotencyClaim(PartnershipIdempotencyOutcome.Replay,
            new PartnershipStoredResponse(entry.StatusCode, entry.ContentType,
                _protector.Unprotect(entry.ProtectedBody!)));
    }

    public async Task CompleteAsync(Guid partnershipId, string idempotencyKey, string requestHash,
        PartnershipStoredResponse response)
    {
        var entry = new PartnershipIdempotencyEntry
        {
            RequestHash = requestHash,
            StatusCode = response.StatusCode,
            ContentType = response.ContentType,
            ProtectedBody = _protector.Protect(response.Body),
        };
        await cache.SetAsync(CacheKey(partnershipId, idempotencyKey), entry, ResponseLifetime);
    }

    public async Task ReleaseAsync(Guid partnershipId, string idempotencyKey) =>
        await cache.RemoveAsync(CacheKey(partnershipId, idempotencyKey));

    private static string CacheKey(Guid partnershipId, string idempotencyKey) =>
        $"{partnershipId:N}:{Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(idempotencyKey)))}";
}
