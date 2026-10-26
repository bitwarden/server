using Bit.Api.KeyManagement.Models.Requests;
using Bit.Api.KeyManagement.Models.Requirements;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.OrganizationAuthorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.KeyManagement.Controllers;

/// <summary>
/// Endpoints for the organization's own key material.
/// </summary>
[Route("organizations/{orgId}/keys")]
[Authorize("Application")]
public class OrganizationKeysController : Controller
{
    private readonly ISetOrganizationKeyIdCommand _setOrganizationKeyIdCommand;

    public OrganizationKeysController(ISetOrganizationKeyIdCommand setOrganizationKeyIdCommand)
    {
        _setOrganizationKeyIdCommand = setOrganizationKeyIdCommand;
    }

    /// <summary>
    /// Reports the key id of the organization's current organization key to the server.
    /// </summary>
    /// <remarks>
    /// This is meant for backfilling the organization key id for existing organizations for which the key id is not
    /// yet recorded. Only Owners and Admins of the organization can call this endpoint.
    /// </remarks>
    [HttpPost("organization-key-id")]
    [Authorize<OrganizationOwnerOrAdminRequirement>]
    public async Task PostOrganizationKeyIdAsync([FromRoute] Guid orgId,
        [FromBody] SetOrganizationKeyIdRequestModel request)
    {
        await _setOrganizationKeyIdCommand.SetOrganizationKeyIdAsync(orgId, request.ToKeyId());
    }
}
