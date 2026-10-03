using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Api.KeyManagement.Models.Responses;
using Bit.Core.Entities;
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
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IApplyOrganizationUserV2UpgradesCommand _applyOrganizationUserV2UpgradesCommand;
    private readonly IAuthorizationService _authorizationService;

    public OrganizationUsersKeysController(
        IOrganizationUserKeyRepository organizationUserKeyRepository,
        IOrganizationUserRepository organizationUserRepository,
        IApplyOrganizationUserV2UpgradesCommand applyOrganizationUserV2UpgradesCommand,
        IAuthorizationService authorizationService)
    {
        _organizationUserKeyRepository = organizationUserKeyRepository;
        _organizationUserRepository = organizationUserRepository;
        _applyOrganizationUserV2UpgradesCommand = applyOrganizationUserV2UpgradesCommand;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Reads the memberships whose account recovery key still wraps the member's V1 user key.
    /// </summary>
    /// <remarks>
    /// A V1 to V2 upgrade rotation cannot re-wrap the account recovery key, because that requires the member to
    /// trust the organization's public key and the upgrade does not prompt the member. The rotation leaves a V2
    /// upgrade token on the membership instead. Account recovery gives the admin the member's V1 user key, so the
    /// admin unwraps the V2 user key from the token and re-wraps the account recovery key with it.
    /// </remarks>
    [HttpGet("pending-v2-upgrades")]
    [Authorize<ManageAccountRecoveryRequirement>]
    [Authorize<ManageUsersRequirement>]
    public async Task<ListResponseModel<OrganizationUserPendingV2UpgradeResponseModel>> GetPendingV2UpgradesAsync(
        [FromRoute] Guid orgId)
    {
        var pending = await _organizationUserKeyRepository.GetManyPendingV2UpgradesByOrganizationIdAsync(orgId);
        var authorizedForRecovery = await GetIdsAuthorizedForAccountRecoveryAsync(
            orgId, pending.Select(details => details.OrganizationUserId));

        var responses = pending
            .Where(details => authorizedForRecovery.Contains(details.OrganizationUserId))
            // A token the server cannot parse is unusable to the admin, so the row is left out of the response.
            .Select(details => new
            {
                Details = details,
                Token = V2UpgradeTokenData.FromJson(details.V2UpgradeToken)
            })
            .Where(row => row.Token is not null)
            .Select(row => new OrganizationUserPendingV2UpgradeResponseModel(row.Details, row.Token!));

        return new ListResponseModel<OrganizationUserPendingV2UpgradeResponseModel>(responses);
    }

    /// <summary>
    /// Replaces the account recovery keys of members who upgraded to V2 encryption, or unenrolls the members
    /// whose entry carries no key.
    /// </summary>
    /// <remarks>
    /// A V2 upgrade token does not always contain a usable user key. The admin then sends no key, which unenrolls
    /// the member from account recovery and clears the token. The member keeps their vault, and the organization's
    /// enrollment policy prompts them to enroll again.
    ///
    /// A membership that changed since the read is skipped rather than rejected, and stays pending.
    /// </remarks>
    [HttpPost("v2-upgrades")]
    [Authorize<ManageAccountRecoveryRequirement>]
    [Authorize<ManageUsersRequirement>]
    public async Task PostV2UpgradesAsync([FromRoute] Guid orgId,
        [FromBody] OrganizationUserV2UpgradesRequestModel model)
    {
        var requestedIds = model.Upgrades.Select(upgrade => upgrade.OrganizationUserId).ToList();
        var authorizedForRecovery = await GetIdsAuthorizedForAccountRecoveryAsync(orgId, requestedIds);

        if (!requestedIds.All(authorizedForRecovery.Contains))
        {
            throw new NotFoundException();
        }

        await _applyOrganizationUserV2UpgradesCommand.ApplyAsync(orgId, model.ToData());
    }

    /// <summary>
    /// Returns the ids of the memberships whose account the caller is authorized to recover.
    /// </summary>
    /// <remarks>
    /// ManageResetPassword alone is not sufficient. An admin can only access the key material of members with equal
    /// or lesser permissions. A Custom user reaches Users and other Custom members only, and an Admin cannot reach
    /// an Owner.
    /// </remarks>
    private async Task<HashSet<Guid>> GetIdsAuthorizedForAccountRecoveryAsync(Guid orgId,
        IEnumerable<Guid> organizationUserIds)
    {
        var ids = organizationUserIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        // Ids reaching PostV2UpgradesAsync come from the request body, and GetManyAsync looks them up by id alone.
        // The handler authorizes against the membership's own organization, so tie it to the route's first.
        var inOrganization = (await _organizationUserRepository.GetManyAsync(ids))
            .Where(organizationUser => organizationUser.OrganizationId == orgId);

        // Authorization is awaited one membership at a time, because the handler reads request-scoped context.
        var authorizations = new List<(Guid OrganizationUserId, bool CanRecoverAccount)>();
        foreach (var organizationUser in inOrganization)
        {
            authorizations.Add((organizationUser.Id, await CanRecoverAccountAsync(organizationUser)));
        }

        return authorizations
            .Where(authorization => authorization.CanRecoverAccount)
            .Select(authorization => authorization.OrganizationUserId)
            .ToHashSet();
    }

    private async Task<bool> CanRecoverAccountAsync(OrganizationUser organizationUser) =>
        (await _authorizationService.AuthorizeAsync(
            User, organizationUser, new RecoverAccountAuthorizationRequirement())).Succeeded;
}
