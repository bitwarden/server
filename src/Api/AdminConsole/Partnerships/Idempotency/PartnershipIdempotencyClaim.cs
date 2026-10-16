namespace Bit.Api.AdminConsole.Partnerships.Idempotency;

public enum PartnershipIdempotencyOutcome
{
    /// <summary>The caller holds the key and must run the action, then complete or release it.</summary>
    Acquired,
    /// <summary>The same request already completed; return <see cref="PartnershipIdempotencyClaim.Response"/>.</summary>
    Replay,
    /// <summary>The key was stored against a different request.</summary>
    KeyReused,
    /// <summary>Another request holds the key.</summary>
    InProgress,
}

public sealed record PartnershipStoredResponse(int StatusCode, string? ContentType, byte[] Body);

public sealed record PartnershipIdempotencyClaim(
    PartnershipIdempotencyOutcome Outcome,
    PartnershipStoredResponse? Response = null);
