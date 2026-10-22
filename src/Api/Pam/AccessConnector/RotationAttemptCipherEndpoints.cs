using Bit.Api.Vault.Models.Request;
using Bit.Api.Vault.Models.Response;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Pam.AccessConnector;

/// <summary>
/// The <c>cipher</c> routes of the connector-facing <c>access-connectors/rotation/attempts</c> resource. They live in
/// <c>Bit.Api</c> because they speak the standard cipher wire contract (<see cref="CipherRequestModel"/> in,
/// <see cref="CipherMiniResponseModel"/> out), which the PAM library cannot reference. The caller maps the group with
/// the access connector machine defaults, whose validation filter runs the request's DataAnnotations before the
/// handler.
/// </summary>
/// <remarks>
/// Scaffold only: the signatures define the wire contract that the generated OpenAPI spec and client bindings are
/// built from. The bodies are intentionally unimplemented — the behavior lands with the rest of the rotation feature.
/// </remarks>
public static class RotationAttemptCipherEndpoints
{
    public static RouteGroupBuilder MapRotationAttemptCipherEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("PamAccessConnectorRotationAttempts");

        group.MapGet("{id:guid}/cipher", GetCipherAsync)
            .WithName("Pam_AccessConnectors_Rotation_Attempts_GetCipher")
            .WithDescription(
                "Returns the cipher of this access connector's claimed, executing attempt, with its full " +
                "encrypted data.")
            // The base type every cipher controller documents, so the schema is the shared one.
            .Produces<CipherMiniResponseModel>();

        group.MapPut("{id:guid}/cipher", PutCipherAsync)
            .WithName("Pam_AccessConnectors_Rotation_Attempts_PutCipher")
            .WithDescription(
                "Writes the rotated cipher (spec AcceptCipherUpdate): a Login cipher request without Data, " +
                "with the item key unchanged and LastKnownRevisionDate from the read. 409: the claim or " +
                "attempt no longer holds, or the revision moved (audited as write_rejected). 404: unknown " +
                "attempt.")
            .Produces(StatusCodes.Status200OK);

        return group;
    }

    public static Task<IResult> GetCipherAsync(Guid id)
        => throw new NotImplementedException();

    public static Task<IResult> PutCipherAsync(Guid id, [FromBody] CipherRequestModel model)
        => throw new NotImplementedException();
}
