namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>A rotation config with its full job and attempt history.</summary>
public class PamRotationConfigDetailResponseModel : PamRotationConfigResponseModel
{
    public PamRotationConfigDetailResponseModel()
        : base("pamRotationConfigDetails")
    {
    }

    /// <summary>Every job recorded against the config, newest first, each with its attempts oldest first.</summary>
    public IReadOnlyList<PamRotationJobResponseModel> Jobs { get; set; } = [];
}
