namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>
/// A rotation daemon's detail view for <c>GET rotation/daemons/{id}</c>: the list shape flattened onto the same
/// object, plus the daemon's recent rotation activity. The fleet surface renders the header from the daemon fields and
/// the activity section from <see cref="Jobs"/>, so both arrive in one response.
/// </summary>
public class PamDaemonDetailResponseModel : PamDaemonResponseModel
{
    public PamDaemonDetailResponseModel()
        : base("pamDaemonDetails")
    {
    }

    /// <summary>
    /// The daemon's recent jobs, newest first, each carrying only the attempts this daemon recorded -- capped by
    /// <c>GetDaemonDetailsQuery</c> rather than being the daemon's whole history.
    /// </summary>
    public IReadOnlyList<PamRotationJobResponseModel> Jobs { get; set; } = [];
}
