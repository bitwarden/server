using Bit.Api.AdminConsole.Authorization;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Api.KeyManagement.Models.Requirements;
using Bit.Api.KeyManagement.Models.Responses;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;
using Bit.HttpExtensions;
using Bit.OrganizationAuthorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.KeyManagement.Controllers;

/// <summary>
/// Endpoints for the key material an organization holds for its members.
/// </summary>
[Route("organizations/{orgId}/users/keys")]
[Authorize("Application")]
public class OrganizationUsersKeysController : Controller
{
    private readonly IOrganizationUserKeyRepository _organizationUserKeyRepository;
    private readonly IApplyOrganizationUserV2UpgradesCommand _applyOrganizationUserV2UpgradesCommand;
    private readonly IOrganizationContext _organizationContext;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IAuthorizationService _authorizationService;

    public OrganizationUsersKeysController(
        IOrganizationUserKeyRepository organizationUserKeyRepository,
        IApplyOrganizationUserV2UpgradesCommand applyOrganizationUserV2UpgradesCommand,
        IOrganizationContext organizationContext,
        IOrganizationUserRepository organizationUserRepository,
        IAuthorizationService authorizationService)
    {
        _organizationUserKeyRepository = organizationUserKeyRepository;
        _applyOrganizationUserV2UpgradesCommand = applyOrganizationUserV2UpgradesCommand;
        _organizationContext = organizationContext;
        _organizationUserRepository = organizationUserRepository;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Reads up to <see cref="OrganizationUserV2UpgradesRequestModel.MaxUpgrades"/> memberships whose account recovery key still wraps the
    /// member's V1 user key.
    /// </summary>
    /// <remarks>
    /// A V1 to V2 upgrade rotation cannot re-wrap the account recovery key, because that requires the member to
    /// trust the organization's public key and the upgrade does not prompt the member. The rotation leaves a V2
    /// upgrade token on the membership instead. Account recovery gives the admin the member's V1 user key, so the
    /// admin unwraps the V2 user key from the token and re-wraps the account recovery key with it.
    ///
    /// Only Owners and Admins of the organization can call this endpoint. It returns only the members the caller can
    /// recover.
    ///
    /// The client posts each page to <c>POST v2-upgrades</c>, then reads the next page with the continuation
    /// token of the response, until the token is null. A page can be empty and still have a token.
    /// </remarks>
    [HttpGet("pending-v2-upgrades")]
    [Authorize<OrganizationOwnerOrAdminRequirement>]
    public async Task<ListResponseModel<OrganizationUserPendingV2UpgradeResponseModel>> GetPendingV2UpgradesAsync(
        [FromRoute] Guid orgId, [FromQuery] Guid? continuationToken)
    {
        var pending = await _organizationUserKeyRepository.GetManyPendingV2UpgradesByOrganizationIdAsync(
            orgId, IsOwner(orgId), continuationToken, OrganizationUserV2UpgradesRequestModel.MaxUpgrades);

        var nextContinuationToken = pending.Count == OrganizationUserV2UpgradesRequestModel.MaxUpgrades
            ? pending.Last().OrganizationUserId.ToString()
            : null;

        var authorizedForRecovery = await GetIdsAuthorizedForAccountRecoveryAsync(
            orgId, pending.Select(details => details.OrganizationUserId));

        var responses = pending
            .Where(details => authorizedForRecovery.Contains(details.OrganizationUserId))
            .Select(details => new OrganizationUserPendingV2UpgradeResponseModel(details,
                V2UpgradeTokenData.FromJson(details.V2UpgradeToken)));

        return new ListResponseModel<OrganizationUserPendingV2UpgradeResponseModel>(responses, nextContinuationToken);
    }

    /// <summary>
    /// Replaces the account recovery keys of members who upgraded to V2 encryption, or unenrolls the members
    /// whose entry carries no key.
    /// </summary>
    /// <remarks>
    /// Only Owners and Admins of the organization can call this endpoint, and only for members they can recover.
    /// A membership that has changed since the read is skipped rather than rejected.
    /// </remarks>
    [HttpPost("v2-upgrades")]
    [Authorize<OrganizationOwnerOrAdminRequirement>]
    public async Task PostV2UpgradesAsync([FromRoute] Guid orgId,
        [FromBody] OrganizationUserV2UpgradesRequestModel model)
    {
        var requested = model.ToData().ToList();
        var authorizedForRecovery = await GetIdsAuthorizedForAccountRecoveryAsync(
            orgId, requested.Select(update => update.OrganizationUserId));

        if (!requested.All(update => authorizedForRecovery.Contains(update.OrganizationUserId)))
        {
            throw new NotFoundException();
        }

        await _applyOrganizationUserV2UpgradesCommand.ApplyAsync(orgId, IsOwner(orgId), requested);
    }

    private async Task<HashSet<Guid>> GetIdsAuthorizedForAccountRecoveryAsync(Guid orgId,
        IEnumerable<Guid> organizationUserIds)
    {
        var ids = organizationUserIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        // The POST body can name memberships of another organization.
        var inOrganization = (await _organizationUserRepository.GetManyAsync(ids))
            .Where(organizationUser => organizationUser.OrganizationId == orgId);

        // Authorization is awaited one membership at a time, because the handler reads request-scoped context.
        var authorized = new HashSet<Guid>();
        foreach (var organizationUser in inOrganization)
        {
            if (await CanRecoverAccountAsync(organizationUser))
            {
                authorized.Add(organizationUser.Id);
            }
        }

        return authorized;
    }

    private async Task<bool> CanRecoverAccountAsync(OrganizationUser organizationUser) =>
        (await _authorizationService.AuthorizeAsync(
            User, organizationUser, new RecoverAccountAuthorizationRequirement())).Succeeded;

    /// <summary>
    /// Only an Owner can access the key material of another Owner.
    /// </summary>
    private bool IsOwner(Guid orgId) =>
        _organizationContext.GetOrganizationClaims(User, orgId)?.Type is OrganizationUserType.Owner;
}
