using  Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;
using Bit.Services.Pam.AccessConnector.Models;

namespace Bit.Services.Pam.AccessConnector.Api.Models.Response;

/// <summary>
/// An access connector's detail view for <c>GET access-connectors/{id}</c>: the list shape flattened onto the same
/// object, plus the access connector's recent rotation activity. The fleet surface renders the header from the access
/// connector fields and the activity section from <see cref="Jobs"/>, so both arrive in one response.
/// </summary>
public class PamAccessConnectorDetailResponseModel : PamAccessConnectorResponseModel
{
    public PamAccessConnectorDetailResponseModel(PamAccessConnectorHistory history)
        : base(history?.Daemon ?? throw new ArgumentNullException(nameof(history)), "pamAccessConnectorDetails")
    {
        Jobs = history.Jobs.Select(job => new PamRotationJobResponseModel(job)).ToList();
    }

    /// <summary>
    /// The access connector's recent jobs, newest first, each carrying only the attempts this access connector recorded
    /// -- capped by <c>GetAccessConnectorDetailsQuery</c> rather than being the access connector's whole history.
    /// </summary>
    public IReadOnlyList<PamRotationJobResponseModel> Jobs { get; set; } = [];
}
