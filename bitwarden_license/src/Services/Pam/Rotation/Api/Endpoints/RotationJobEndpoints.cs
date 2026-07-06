using Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;

namespace Bit.Services.Pam.Rotation.Api.Endpoints;

/// <summary>The daemon-facing <c>rotation/jobs</c> resource: claiming.</summary>
internal static class RotationJobEndpoints
{
    public static RouteGroupBuilder MapRotationJobEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("PamRotationJobs");

        group.MapPost("{id:guid}/claim", (Guid id, RotationJobEndpointsHandler handler) => handler.Claim(id))
            .WithName("Pam_Rotation_Jobs_Claim")
            .WithDescription(
                "Atomically claims a job -- first-claim-wins -- and returns the work snapshot needed to execute " +
                "the rotation. 409 means another daemon won the race (claim a different job); 404 means this " +
                "daemon was never eligible to claim it (no assignment, wrong organization, or a disabled " +
                "target/config).");

        return group;
    }
}
