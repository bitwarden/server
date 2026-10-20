using Bit.Api.Vault.Models.Request;
using Bit.Api.Vault.Models.Response;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Pam.AccessConnector.Commands;
using Bit.Core.Pam.AccessConnector.Queries;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Vault.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Pam.AccessConnector;

/// <summary>
/// The <c>cipher</c> routes of the connector-facing <c>access-connectors/rotation/attempts</c> resource. They live in
/// <c>Bit.Api</c> because they speak the standard cipher wire contract (<see cref="CipherRequestModel"/> in,
/// <see cref="CipherMiniResponseModel"/> out), which the PAM library cannot reference. They depend only on Core
/// interfaces, so this compiles in OSS builds. The caller maps the group with the access connector machine defaults,
/// whose validation filter runs the request's DataAnnotations before the handler.
/// </summary>
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

    public static async Task<IResult> GetCipherAsync(
        Guid id,
        ICurrentContext currentContext,
        IGetRotationCipherQuery getRotationCipherQuery,
        IOrganizationRepository organizationRepository,
        IGlobalSettings globalSettings)
    {
        var (cipher, access) = await getRotationCipherQuery.GetAsync(currentContext.PamAccessConnectorId!.Value, id);

        // OrganizationAbility lacks UseTotp.
        var organization = cipher.OrganizationId is { } organizationId
            ? await organizationRepository.GetByIdAsync(organizationId)
            : null;

        return TypedResults.Json(
            new FullCipherMiniResponseModel(access, cipher, globalSettings, organization?.UseTotp ?? false));
    }

    public static async Task<IResult> PutCipherAsync(
        Guid id,
        [FromBody] CipherRequestModel model,
        ICurrentContext currentContext,
        IGetRotationCipherQuery getRotationCipherQuery,
        ISubmitCipherUpdateCommand submitCipherUpdateCommand)
    {
        var accessConnectorId = currentContext.PamAccessConnectorId!.Value;

        // Capability check first, so a body is never validated against a cipher the connector cannot see.
        var (existing, _) = await getRotationCipherQuery.GetAsync(accessConnectorId, id);

        ValidateRequest(model, existing);

        // The server builds Data from the structured fields; the validated item key is never stored.
        model.ToCipher(existing);

        await submitCipherUpdateCommand.SubmitAsync(
            accessConnectorId, id, existing.Data, model.LastKnownRevisionDate!.Value);
        return TypedResults.Ok();
    }

    private static void ValidateRequest(CipherRequestModel model, Core.Vault.Entities.Cipher existing)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (existing.Type != CipherType.Login || model.Type != CipherType.Login)
        {
            throw new BadRequestException("Only Login ciphers can be rotated.");
        }

        if (!Guid.TryParse(model.OrganizationId, out var organizationId) || organizationId != existing.OrganizationId)
        {
            throw new BadRequestException("The cipher's organization cannot be changed.");
        }

        // Only Data is written. A keyless cipher would make the client generate a new item key, orphaning attachment
        // keys, and a changed key would not match them.
        if (string.IsNullOrEmpty(existing.Key))
        {
            throw new BadRequestException("A cipher without an item key cannot be rotated.");
        }

        if (!string.Equals(model.Key, existing.Key, StringComparison.Ordinal))
        {
            throw new BadRequestException("The cipher's item key cannot be changed.");
        }

        if (!string.IsNullOrWhiteSpace(model.Data))
        {
            throw new BadRequestException("Data must not be provided; send the structured cipher fields.");
        }

        if (model.LastKnownRevisionDate is null)
        {
            throw new BadRequestException("LastKnownRevisionDate is required.");
        }
    }
}
