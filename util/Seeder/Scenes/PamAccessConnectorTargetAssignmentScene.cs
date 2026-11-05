using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Lets a PAM access connector claim rotation jobs on a target system. Both must belong to the given organization.
/// </summary>
public class PamAccessConnectorTargetAssignmentScene(
    IPamAccessConnectorRepository accessConnectorRepository,
    IPamTargetSystemRepository targetSystemRepository,
    IManglerService manglerService)
    : IScene<PamAccessConnectorTargetAssignmentScene.Request, PamAccessConnectorTargetAssignmentScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        public required Guid AccessConnectorId { get; set; }
        [Required]
        public required Guid TargetSystemId { get; set; }
    }

    public class Result
    {
        public required Guid AssignmentId { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        var accessConnector = await accessConnectorRepository.GetByIdAsync(request.AccessConnectorId);
        if (accessConnector == null || accessConnector.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException(
                $"Access connector {request.AccessConnectorId} not found in organization {request.OrganizationId}.");
        }

        var target = await targetSystemRepository.GetByIdAsync(request.TargetSystemId);
        if (target == null || target.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException(
                $"Target system {request.TargetSystemId} not found in organization {request.OrganizationId}.");
        }

        // CreateAssignmentAsync does not assign an id.
        var assignment = new PamAccessConnectorTargetAssignment
        {
            Id = CombGuid.Generate(),
            AccessConnectorId = request.AccessConnectorId,
            TargetSystemId = request.TargetSystemId,
            OrganizationId = request.OrganizationId
        };
        await accessConnectorRepository.CreateAssignmentAsync(assignment);

        return new SceneResult<Result>(
            result: new Result { AssignmentId = assignment.Id },
            mangleMap: manglerService.GetMangleMap());
    }
}
