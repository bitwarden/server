namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// The range an Item-filter read covers, as query parameters.
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
