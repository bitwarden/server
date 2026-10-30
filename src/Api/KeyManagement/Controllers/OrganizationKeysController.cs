using Bit.Api.KeyManagement.Models.Requests;
using Bit.Api.KeyManagement.Models.Requirements;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.Repositories;
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
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ICurrentContext _currentContext;

    public OrganizationKeysController(
        ISetOrganizationKeyIdCommand setOrganizationKeyIdCommand,
        IOrganizationUserRepository organizationUserRepository,
        ICurrentContext currentContext)
    {
        _setOrganizationKeyIdCommand = setOrganizationKeyIdCommand;
        _organizationUserRepository = organizationUserRepository;
        _currentContext = currentContext;
    }

    /// <summary>
    /// Reports the key id of the organization's current organization key to the server.
    /// </summary>
    /// <remarks>
    /// This is meant for backfilling the organization key id for existing organizations for which the key id is not
    /// yet recorded. Only Owners and Admins of the organization who hold the organization key can call this endpoint.
    /// </remarks>
    [HttpPost("organization-key-id")]
    [Authorize<OrganizationOwnerOrAdminRequirement>]
    public async Task PostOrganizationKeyIdAsync([FromRoute] Guid orgId,
        [FromBody] SetOrganizationKeyIdRequestModel request)
    {
        var userId = _currentContext.UserId!.Value;

        // Only a member who holds the organization key can know its key id.
        var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(orgId, userId);
        if (string.IsNullOrWhiteSpace(organizationUser?.Key))
        {
            throw new BadRequestException("Organization key is not available.");
        }

        await _setOrganizationKeyIdCommand.SetOrganizationKeyIdAsync(orgId, request.ToKeyId());
    }
}
