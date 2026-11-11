using Bit.Services.Pam.Models;
namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// A request to lease the routed cipher. Supply <see cref="DurationSeconds"/> on the automatic path, or
/// <see cref="Start"/>, <see cref="End"/> and <see cref="Reason"/> on the human path; the pre-check reports which
/// path applies.
/// </summary>
public class AccessRequestCreateRequestModel
{
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// The start of the requested window. A timestamp with neither <c>Z</c> nor an offset is read as UTC.
    /// </summary>
    public DateTime? Start { get; set; }

    /// <summary>The end of the requested window, read like <see cref="Start"/>.</summary>
    public DateTime? End { get; set; }

    public string? Reason { get; set; }

    public AccessRequestSubmission ToSubmission() => new()
    {
        DurationSeconds = DurationSeconds,
        Start = Start.ToUtc(),
        End = End.ToUtc(),
        Reason = Reason,
    };
}
