using Bit.Services.Pam.Rotation.Models;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>A rotation config's detail view: the config summary together with its full job/attempt history, oldest first.</summary>
public class PamRotationConfigDetailResponseModel
{
    public PamRotationConfigDetailResponseModel(PamRotationConfigHistory history, bool awaitingManualRotation)
    {
        ArgumentNullException.ThrowIfNull(history);

        Config = new PamRotationConfigResponseModel(history.Config, awaitingManualRotation);
        Jobs = history.Jobs.Select(job => new PamRotationJobResponseModel(job)).ToList();
    }

    public PamRotationConfigResponseModel Config { get; }

    /// <summary>Every job recorded against the config, oldest first, each carrying its own attempts.</summary>
    public IReadOnlyList<PamRotationJobResponseModel> Jobs { get; }
}
