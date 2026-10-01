using Bit.Services.Pam.AccessConnector.Models;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>
/// A rotation config's detail view: the list shape flattened onto the same object, plus the config's full job/attempt
/// history. The managed-credential surface renders its header from the config fields and the history section from
/// <see cref="Jobs"/>, so both arrive in one response -- the same shape
/// <see cref="Bit.Services.Pam.AccessConnector.Api.Models.Response.PamAccessConnectorDetailResponseModel"/> returns for
/// an access connector.
/// </summary>
public class PamRotationConfigDetailResponseModel : PamRotationConfigResponseModel
{
    public PamRotationConfigDetailResponseModel(PamRotationConfigHistory history, bool awaitingManualRotation)
        : base(
            history?.Config ?? throw new ArgumentNullException(nameof(history)),
            awaitingManualRotation,
            "pamRotationConfigDetails")
    {
        Jobs = history.Jobs.Select(job => new PamRotationJobResponseModel(job)).ToList();
    }

    /// <summary>
    /// Every job recorded against the config, newest first, each carrying its own attempts (oldest first).
    /// </summary>
    public IReadOnlyList<PamRotationJobResponseModel> Jobs { get; set; } = [];
}
