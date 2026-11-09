using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Creates a PAM target system in the <see cref="PamTargetSystemStatus.PendingSeal"/> state with no sealed data,
/// as <c>RegisterTargetSystemCommand</c> does. Compose with <see cref="PamTargetSystemSealScene"/> to activate it.
/// </summary>
public class PamTargetSystemScene(
    IOrganizationRepository organizationRepository,
    IPamTargetSystemRepository targetSystemRepository,
    IManglerService manglerService) : IScene<PamTargetSystemScene.Request, PamTargetSystemScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        public required string Name { get; set; }
        public PamTargetSystemMethod Method { get; set; } = PamTargetSystemMethod.Automatic;
    }

    public class Result
    {
        public required Guid TargetSystemId { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        if (await organizationRepository.GetByIdAsync(request.OrganizationId) == null)
        {
            throw new InvalidOperationException($"Organization {request.OrganizationId} not found.");
        }

        var target = await targetSystemRepository.CreateAsync(new PamTargetSystem
        {
            OrganizationId = request.OrganizationId,
            Name = request.Name,
            Method = request.Method,
            SealedData = null,
            WrappedKey = null,
            Status = PamTargetSystemStatus.PendingSeal
        });

        return new SceneResult<Result>(
            result: new Result { TargetSystemId = target.Id },
            mangleMap: manglerService.GetMangleMap());
    }
}
