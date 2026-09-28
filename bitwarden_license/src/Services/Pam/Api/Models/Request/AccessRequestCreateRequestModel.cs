using Bit.Services.Pam.Models;
namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// A request to lease a cipher. Supply <see cref="DurationSeconds"/> for the automatic path, or
/// <see cref="Start"/>/<see cref="End"/> + <see cref="Reason"/> for the human path. The server validates the shape
/// against the cipher's resolved approval outcome (run a pre-check first). The cipher is identified by the route.
/// </summary>
public class AccessRequestCreateRequestModel
{
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// The start of the requested window. A timestamp with neither <c>Z</c> nor an offset is read as UTC.
    /// </summary>
    public DateTime? Start { get; set; }

    /// <summary>
    /// The end of the requested window, read like <see cref="Start"/>.
    /// </summary>
    public DateTime? End { get; set; }

    public string? Reason { get; set; }

    /// <summary>
    /// Projects the wire model onto the command's submission, normalising the window to UTC.
    /// </summary>
    public AccessRequestSubmission ToSubmission() => new()
    {
        DurationSeconds = DurationSeconds,
        Start = Start.ToUtc(),
        End = End.ToUtc(),
        Reason = Reason,
    };
}
