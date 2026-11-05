using System.ComponentModel.DataAnnotations;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Stores the sealed data and wrapped key on an existing PAM target system and sets its status (default
/// <see cref="PamTargetSystemStatus.Active"/>). Re-run with <c>Status = Disabled</c> to disable a target after
/// seeding work against it.
/// </summary>
public class PamTargetSystemSealScene(
    IPamTargetSystemRepository targetSystemRepository,
    IManglerService manglerService) : IScene<PamTargetSystemSealScene.Request, PamTargetSystemSealScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid TargetSystemId { get; set; }
        [Required]
        public required string SealedData { get; set; }
        [Required]
        public required string WrappedKey { get; set; }
        public PamTargetSystemStatus Status { get; set; } = PamTargetSystemStatus.Active;
    }

    public class Result
    {
        public required Guid TargetSystemId { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        var target = await targetSystemRepository.GetByIdAsync(request.TargetSystemId)
            ?? throw new InvalidOperationException($"Target system {request.TargetSystemId} not found.");

        target.SealedData = request.SealedData;
        target.WrappedKey = request.WrappedKey;
        target.Status = request.Status;
        target.RevisionDate = DateTime.UtcNow;
        await targetSystemRepository.ReplaceAsync(target);

        return new SceneResult<Result>(
            result: new Result { TargetSystemId = target.Id },
            mangleMap: manglerService.GetMangleMap());
    }
}
