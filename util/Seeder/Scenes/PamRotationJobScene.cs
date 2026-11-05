using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Offers an unclaimed on-demand rotation job for a PAM rotation config through the same guarded insert the server
/// uses. Fails at seed time when the guard refuses (config disabled, target not Automatic and Active, or a job is
/// already active). A negative <see cref="Request.ExpiresInSeconds"/> seeds an already-expired job.
/// </summary>
public class PamRotationJobScene(
    IPamRotationConfigRepository rotationConfigRepository,
    IPamRotationJobRepository rotationJobRepository,
    IManglerService manglerService) : IScene<PamRotationJobScene.Request, PamRotationJobScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid RotationConfigId { get; set; }
        public int ExpiresInSeconds { get; set; } = 3600;
        public int NextClaimableInSeconds { get; set; }
    }

    public class Result
    {
        public required Guid JobId { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        if (await rotationConfigRepository.GetByIdAsync(request.RotationConfigId) == null)
        {
            throw new InvalidOperationException($"Rotation config {request.RotationConfigId} not found.");
        }

        var now = DateTime.UtcNow;
        // CreateGuardedAsync does not assign an id.
        var job = new PamRotationJob
        {
            Id = CombGuid.Generate(),
            RotationConfigId = request.RotationConfigId,
            Source = PamRotationSource.OnDemand,
            Action = PamRotationJobAction.None,
            CreationDate = now,
            NextClaimableDate = now.AddSeconds(request.NextClaimableInSeconds),
            ExpirationDate = now.AddSeconds(request.ExpiresInSeconds)
        };

        var outcome = await rotationJobRepository.CreateGuardedAsync(job);
        if (outcome != PamRotationJobCreateOutcome.Created)
        {
            throw new InvalidOperationException(
                $"Could not seed a rotation job for config {request.RotationConfigId}: {outcome} " +
                "(the config must be enabled on an Automatic, Active target and have no active job).");
        }

        return new SceneResult<Result>(
            result: new Result { JobId = job.Id },
            mangleMap: manglerService.GetMangleMap());
    }
}
