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
    /// The start of the requested window. Send an instant — either <c>Z</c>-suffixed or with an explicit offset; a
    /// timestamp carrying neither is read as UTC. Normalised to UTC by <see cref="ToSubmission"/>, since the window
    /// is stored and compared as a UTC instant.
    /// </summary>
    public DateTime? Start { get; set; }

    /// <summary>
    /// The end of the requested window, under the same UTC contract as <see cref="Start"/>.
    /// </summary>
    public DateTime? End { get; set; }

    public string? Reason { get; set; }

    /// <summary>
    /// Projects the wire model onto the submission the command consumes, pinning the window to UTC on the way — see
    /// <see cref="PamRequestDateTimeExtensions"/> for why the serializer's own answer cannot be persisted directly.
    /// </summary>
    public AccessRequestSubmission ToSubmission() => new()
    {
        DurationSeconds = DurationSeconds,
        Start = Start.ToUtc(),
        End = End.ToUtc(),
        Reason = Reason,
    };
}
