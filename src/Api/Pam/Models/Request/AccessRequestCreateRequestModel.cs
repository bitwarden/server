using Bit.Commercial.Pam.Models;
namespace Bit.Api.Pam.Models.Request;

/// <summary>
/// A request to lease a cipher. Supply <see cref="DurationSeconds"/> for the automatic path, or
/// <see cref="Start"/>/<see cref="End"/> + <see cref="Reason"/> for the human path. The server validates the shape
/// against the cipher's resolved approval outcome (run a pre-check first). The cipher is identified by the route.
/// </summary>
public class AccessRequestCreateRequestModel
{
    public int? DurationSeconds { get; set; }

    public DateTime? Start { get; set; }

    public DateTime? End { get; set; }

    public string? Reason { get; set; }

    public AccessRequestSubmission ToSubmission() => new()
    {
        DurationSeconds = DurationSeconds,
        Start = Start,
        End = End,
        Reason = Reason,
    };
}
