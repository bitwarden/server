using System.Net;
using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationScopedApiKeys.Interfaces;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.HttpExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations/{orgId}/scoped-api-keys")]
[Authorize("Application")]
[Bitwarden.Server.Sdk.Features.RequireFeature(FeatureFlagKeys.ScopedOrganizationApiKeys)]
public class OrganizationScopedApiKeysController(
    ICurrentContext currentContext,
    IUserService userService,
    ICreateOrganizationScopedApiKeyCommand createOrganizationScopedApiKeyCommand,
    IGetOrganizationScopedApiKeysQuery getOrganizationScopedApiKeysQuery,
    IRevokeOrganizationScopedApiKeyCommand revokeOrganizationScopedApiKeyCommand)
    : BaseAdminConsoleController
{
    [HttpGet("")]
    [NoopAuthorize]
    [ProducesResponseType(typeof(ListResponseModel<OrganizationScopedApiKeyResponseModel>), (int)HttpStatusCode.OK)]
    public async Task<IResult> GetAll([FromRoute] Guid orgId)
    {
        if (!await currentContext.OrganizationOwner(orgId))
        {
            throw new NotFoundException();
        }

        var apiKeys = await getOrganizationScopedApiKeysQuery.GetManyByOrganizationIdAsync(orgId);

        return TypedResults.Ok(new ListResponseModel<OrganizationScopedApiKeyResponseModel>(
            apiKeys.Select(k => new OrganizationScopedApiKeyResponseModel(k))));
    }

    [HttpPost("")]
    [NoopAuthorize]
    [ProducesResponseType(typeof(OrganizationScopedApiKeyCreatedResponseModel), (int)HttpStatusCode.OK)]
    public async Task<IResult> Create([FromRoute] Guid orgId,
        [FromBody] CreateOrganizationScopedApiKeyRequestModel model)
    {
        if (!await currentContext.OrganizationOwner(orgId))
        {
            throw new NotFoundException();
        }

        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        if (!await userService.VerifySecretAsync(user, model.Secret))
        {
            await Task.Delay(2000);
            throw new BadRequestException("MasterPasswordHash", "Invalid password.");
        }

        var result = await createOrganizationScopedApiKeyCommand.CreateAsync(model.ToCommandRequest(orgId));

        return Handle(result, created =>
        {
            Response.Headers.CacheControl = "no-store";
            return TypedResults.Ok(new OrganizationScopedApiKeyCreatedResponseModel(created));
        });
    }

    [HttpDelete("{id}")]
    [NoopAuthorize]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IResult> Revoke([FromRoute] Guid orgId, [FromRoute] Guid id)
    {
        if (!await currentContext.OrganizationOwner(orgId))
        {
            throw new NotFoundException();
        }

        var result = await revokeOrganizationScopedApiKeyCommand.RevokeAsync(orgId, id);
        return Handle(result);
    }
}
