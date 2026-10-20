namespace Bit.Api.AdminConsole.Partnerships.Idempotency;

/// <summary>
/// The cached state of one partnership's <c>Idempotency-Key</c>: in flight while <see cref="InFlightId"/> is set,
/// otherwise the stored response of the first request.
/// </summary>
public sealed record PartnershipIdempotencyEntry
{
    /// <summary>
    /// SHA-256 of the request method, path, query, and body. Never the raw request.
    /// </summary>
    public required string RequestHash { get; init; }

    /// <summary>
    /// Identifies the request that holds the key while it runs. Null once the response is stored.
    /// </summary>
    public Guid? InFlightId { get; init; }

    public int StatusCode { get; init; }
    public string? ContentType { get; init; }

    /// <summary>
    /// The response body protected with ASP.NET Core Data Protection, because it can contain the partner's external
    /// identifier.
    /// </summary>
    public byte[]? ProtectedBody { get; init; }
}
