namespace Bit.Services.Pam.Models;

/// <summary>
/// A request to lease a cipher. The automatic path supplies <see cref="DurationSeconds"/> (and an optional
/// <see cref="Reason"/>); the human path supplies a <see cref="Start"/>/<see cref="End"/> window and a required
/// <see cref="Reason"/>. The command validates the shape against the cipher's resolved approval outcome.
/// </summary>
public sealed class AccessRequestSubmission
{
    public int? DurationSeconds { get; init; }

    /// <summary>
    /// The start of the requested window, as a UTC instant. The command pins it onto the request unchanged and
    /// compares it against a <c>UtcNow</c> reading, so callers building a submission off the wire normalise first
    /// (the API model does this in <c>ToSubmission</c>).
    /// </summary>
    public DateTime? Start { get; init; }

    /// <summary>
    /// The end of the requested window, as a UTC instant. Same contract as <see cref="Start"/>.
    /// </summary>
    public DateTime? End { get; init; }

    public string? Reason { get; init; }
}
