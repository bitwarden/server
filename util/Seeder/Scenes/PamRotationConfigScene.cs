using System.ComponentModel.DataAnnotations;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Creates a PAM rotation config for an organization cipher on a target system. The next rotation date is far in
/// the future and there is no schedule, so the sweep never offers a job of its own; seed jobs with
/// <see cref="PamRotationJobScene"/>.
/// </summary>
public class PamRotationConfigScene(
    ICipherRepository cipherRepository,
    IPamTargetSystemRepository targetSystemRepository,
    IPamRotationConfigRepository rotationConfigRepository,
    IManglerService manglerService) : IScene<PamRotationConfigScene.Request, PamRotationConfigScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        public required Guid CipherId { get; set; }
        [Required]
        public required Guid TargetSystemId { get; set; }
        [Required]
        public required string SealedData { get; set; }
        [Required]
        public required string WrappedKey { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public class Result
    {
        public required Guid RotationConfigId { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        var cipher = await cipherRepository.GetByIdAsync(request.CipherId);
        if (cipher == null || cipher.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException(
                $"Cipher {request.CipherId} not found in organization {request.OrganizationId}.");
        }

        var target = await targetSystemRepository.GetByIdAsync(request.TargetSystemId);
        if (target == null || target.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException(
                $"Target system {request.TargetSystemId} not found in organization {request.OrganizationId}.");
        }

        var config = await rotationConfigRepository.CreateAsync(new PamRotationConfig
        {
            OrganizationId = request.OrganizationId,
            CipherId = request.CipherId,
            TargetSystemId = request.TargetSystemId,
            SealedData = request.SealedData,
            WrappedKey = request.WrappedKey,
            ScheduleCron = null,
            NextRotationDate = DateTime.UtcNow.AddYears(10),
            Enabled = request.Enabled
        });

        return new SceneResult<Result>(
            result: new Result { RotationConfigId = config.Id },
            mangleMap: manglerService.GetMangleMap());
    }
}
