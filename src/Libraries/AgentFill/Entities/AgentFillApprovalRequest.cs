namespace Bit.AgentFill.Entities;

/// <summary>
/// One agent fill approval request and at most one response. The server stores the sealed strings without opening
/// them: they must never be logged, parsed or inspected.
/// </summary>
internal sealed class AgentFillApprovalRequest
{
    /// <summary>How long a request can be answered, matching the SDK's and the desktop app's approval expiry.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RequestDeviceId { get; set; }
    public string SealedRequest { get; set; } = null!;
    public string? SealedResponse { get; set; }
    public Guid? ResponseDeviceId { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public DateTime? ResponseDate { get; set; }

    public bool IsAnswered => SealedResponse is not null;

    public bool IsExpiredAt(DateTime now) => !IsAnswered && ExpirationDate <= now;
}
