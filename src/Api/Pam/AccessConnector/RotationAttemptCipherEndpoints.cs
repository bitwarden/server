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
/// <c>Bit.Api</c> rather than the PAM library because they speak the same cipher wire contract as every other client
/// (<see cref="CipherRequestModel"/> in, <see cref="CipherMiniResponseModel"/> out), and the PAM library cannot
/// reference these types. They depend only on Core interfaces whose implementations the PAM library provides, so this
/// compiles in OSS builds; the caller maps the group with the access connector machine defaults, whose validation
/// filter runs the DataAnnotations of the request before the handler. The handler keeps the business rules.
/// </summary>
public static class RotationAttemptCipherEndpoints
{
    public static RouteGroupBuilder MapRotationAttemptCipherEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("PamAccessConnectorRotationAttempts");

        group.MapGet("{id:guid}/cipher", GetCipherAsync)
            .WithName("Pam_AccessConnectors_Rotation_Attempts_GetCipher")
            .WithDescription(
                "Returns the cipher for this access connector's claimed, executing attempt only -- never a " +
                "general cipher read. The shape is the standard cipher response, with the full encrypted Data.")
            // The abstract base is what every cipher controller documents, so the schema is the shared one.
            .Produces<CipherMiniResponseModel>();

        group.MapPut("{id:guid}/cipher", PutCipherAsync)
            .WithName("Pam_AccessConnectors_Rotation_Attempts_PutCipher")
            .WithDescription(
                "Writes the rotated secret back to the cipher (spec AcceptCipherUpdate) via an atomic capability " +
                "check. The body is the standard cipher request: a Login cipher with the structured fields, no " +
                "Data, the same OrganizationId, and LastKnownRevisionDate set to the revision date " +
                "read before rotating. The server builds the stored Data itself and stores the request's " +
                "Key with it in the same write; anything else is a 400, as is a cipher with attachments " +
                "and no item key. 409 means the claim/attempt no " +
                "longer holds, or LastKnownRevisionDate no longer matches the cipher's current revision (a " +
                "concurrent user edit won) -- audited as write_rejected. " +
                "404 means the attempt id is unknown (no audit).")
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

        // OrganizationAbility does not carry UseTotp, so read it from the organization, as the cipher queries do.
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

        // Capability check and 404s first, so a body is never validated against a cipher the connector cannot see.
        var (existing, _) = await getRotationCipherQuery.GetAsync(accessConnectorId, id);

        ValidateRequest(model, existing);

        // Builds Data from the structured fields, as every other cipher write does, and takes the request's key: the
        // client encrypts under a freshly wrapped item key, which the write stores together with the data.
        model.ToCipher(existing);

        await submitCipherUpdateCommand.SubmitAsync(
            accessConnectorId, id, existing.Data, existing.Key, model.LastKnownRevisionDate!.Value);
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

        // The client generates an item key for a keyless cipher and re-wraps the attachment keys under it, but this
        // route persists only Data and Key, so the stored attachment keys would no longer unwrap.
        if (string.IsNullOrEmpty(existing.Key) && (existing.GetAttachments()?.Count ?? 0) > 0)
        {
            throw new BadRequestException("A cipher with attachments and no item key cannot be rotated.");
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
