using Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;
using Bit.Services.Pam.Rotation.Api.Models.Request;

namespace Bit.Services.Pam.Rotation.Api.Endpoints;

/// <summary>
/// The daemon-facing <c>rotation/attempts</c> resource: reading and writing back a claimed attempt's cipher, and
/// reporting its outcome.
/// </summary>
internal static class RotationAttemptEndpoints
{
    public static RouteGroupBuilder MapRotationAttemptEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("PamRotationAttempts");

        group.MapGet("{id:guid}/cipher", (Guid id, RotationAttemptEndpointsHandler handler) => handler.GetCipher(id))
            .WithName("Pam_Rotation_Attempts_GetCipher")
            .WithDescription(
                "Returns the cipher for this daemon's claimed, executing attempt only -- never a general cipher " +
                "read. Data is returned exactly as stored: opaque ciphertext the server never decrypts.");

        group.MapPut("{id:guid}/cipher",
            async (Guid id, SubmitCipherUpdateRequestModel model, RotationAttemptEndpointsHandler handler) =>
            {
                await handler.PutCipher(id, model);
                return TypedResults.Ok();
            })
            .WithName("Pam_Rotation_Attempts_PutCipher")
            .WithDescription(
                "Writes the rotated secret back to the cipher (spec AcceptCipherUpdate) via an atomic capability " +
                "check. 409 means the claim/attempt no longer holds, or LastKnownRevisionDate no longer matches " +
                "the cipher's current revision (a concurrent user edit won) -- audited as write_rejected. 404 means " +
                "the attempt id is unknown (no audit).");

        group.MapPost("{id:guid}/success",
            async (Guid id, ReportRotationSucceededRequestModel model, RotationAttemptEndpointsHandler handler) =>
            {
                await handler.Success(id, model);
                return TypedResults.NoContent();
            })
            .WithName("Pam_Rotation_Attempts_Success")
            .WithDescription(
                "Reports a successful rotation (spec RecordRotationSucceeded). Requires the attempt to already " +
                "have a written cipher (the VerifiedBeforeSuccess backstop); otherwise the report is treated as " +
                "stale (409, audited as report_rejected).");

        group.MapPost("{id:guid}/failure",
            async (Guid id, ReportRotationFailedRequestModel model, RotationAttemptEndpointsHandler handler) =>
            {
                await handler.Failure(id, model);
                return TypedResults.NoContent();
            })
            .WithName("Pam_Rotation_Attempts_Failure")
            .WithDescription(
                "Reports a failed rotation attempt (spec RecordRotationFailed). Never forward raw target-system " +
                "error output as ErrorCode/Detail -- it can echo credentials. Send a bounded error code plus an " +
                "optional short detail instead; both are truncated (never rejected) server-side.");

        return group;
    }
}
