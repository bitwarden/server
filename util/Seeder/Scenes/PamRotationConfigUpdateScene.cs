using System.ComponentModel.DataAnnotations;
using Bit.Pam.Repositories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Enables or disables an existing PAM rotation config, e.g. to disable it after a job was seeded (the job guard
/// refuses to create jobs on a disabled config).
/// </summary>
public class PamRotationConfigUpdateScene(
    IPamRotationConfigRepository rotationConfigRepository,
    IManglerService manglerService) : IScene<PamRotationConfigUpdateScene.Request, PamRotationConfigUpdateScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid RotationConfigId { get; set; }
        [Required]
        public required bool Enabled { get; set; }
    }

    public class Result
    {
        public required Guid RotationConfigId { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        var config = await rotationConfigRepository.GetByIdAsync(request.RotationConfigId)
            ?? throw new InvalidOperationException($"Rotation config {request.RotationConfigId} not found.");

        config.Enabled = request.Enabled;
        config.RevisionDate = DateTime.UtcNow;
        await rotationConfigRepository.ReplaceAsync(config);

        return new SceneResult<Result>(
            result: new Result { RotationConfigId = config.Id },
            mangleMap: manglerService.GetMangleMap());
    }
}
