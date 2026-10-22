using Bit.Services.Pam.AccessConnector.Models;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Api.Models.Response;

/// <summary>An access connector with its recent rotation activity.</summary>
public class PamAccessConnectorDetailResponseModel : PamAccessConnectorResponseModel
{
    public PamAccessConnectorDetailResponseModel(PamAccessConnectorHistory history)
        : base(
            history?.AccessConnector ?? throw new ArgumentNullException(nameof(history)), "pamAccessConnectorDetails")
    {
        Jobs = history.Jobs.Select(job => new PamRotationJobResponseModel(job)).ToList();
    }

    /// <summary>
    /// The access connector's most recent jobs (capped), newest first, each with only the attempts it recorded.
    /// </summary>
    public IReadOnlyList<PamRotationJobResponseModel> Jobs { get; set; } = [];
}
