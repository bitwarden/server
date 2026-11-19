using Bit.Services.Pam.AccessConnector.Models;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>A rotation config with its full job and attempt history.</summary>
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

    /// <summary>Every job recorded against the config, newest first, each with its attempts oldest first.</summary>
    public IReadOnlyList<PamRotationJobResponseModel> Jobs { get; set; } = [];
}
