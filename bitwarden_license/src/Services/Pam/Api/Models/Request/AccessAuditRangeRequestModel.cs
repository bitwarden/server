namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// The range an Item-filter read covers, as query parameters. The same two bounds the trail read takes, and
/// deliberately only those two: the menu follows the time period an auditor has chosen, because that is what changes
/// which items exist, but not the other dimensions — narrowing to one actor should not quietly remove the credentials
/// they never touched from a menu the auditor is using to look for exactly that.
/// </summary>
public class AccessAuditRangeRequestModel
{
    /// <summary>
    /// Inclusive lower bound on the event's instant. Absent reaches back as far as the retention window allows.
    /// </summary>
    public DateTime? Start { get; set; }

    /// <summary>Inclusive upper bound on the event's instant. Absent reaches up to now.</summary>
    public DateTime? End { get; set; }

    public (DateTime? Start, DateTime? End) ToRange() => (Start.ToUtc(), End.ToUtc());
}
