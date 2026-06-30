namespace Bit.Commercial.Pam.Models;

/// <summary>
/// A request to lease a cipher. The automatic path supplies <see cref="DurationSeconds"/> (and an optional
/// <see cref="Reason"/>); the human path supplies a <see cref="Start"/>/<see cref="End"/> window and a required
/// <see cref="Reason"/>. The command validates the shape against the cipher's resolved approval outcome.
/// </summary>
public sealed class AccessRequestSubmission
{
    public int? DurationSeconds { get; init; }
    public DateTime? Start { get; init; }
    public DateTime? End { get; init; }
    public string? Reason { get; init; }
}
