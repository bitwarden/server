namespace Bit.Services.Pam.Models;

/// <summary>
/// A request to lease a cipher: a <see cref="DurationSeconds"/> on the automatic path, or a
/// <see cref="Start"/>/<see cref="End"/> window and a <see cref="Reason"/> on the human path.
/// </summary>
public sealed class AccessRequestSubmission
{
    public int? DurationSeconds { get; init; }

    /// <summary>
    /// The start of the requested window, as a UTC instant.
    /// </summary>
    public DateTime? Start { get; init; }

    /// <summary>
    /// The end of the requested window, as a UTC instant.
    /// </summary>
    public DateTime? End { get; init; }

    public string? Reason { get; init; }
}
