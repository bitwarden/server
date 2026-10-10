using Bit.Api.AdminConsole.Authorization;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Api.KeyManagement.Models.Requirements;
using Bit.Api.KeyManagement.Models.Responses;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.HttpExtensions;
using Bit.OrganizationAuthorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.KeyManagement.Controllers;

/// <summary>
/// Endpoints for the key material an organization holds for its members.
/// </summary>
/// <remarks>
/// Only Owners and Admins of the organization can use these endpoints. A provider user who manages the organization
/// without being a member cannot. An Admin cannot access the key material of an Owner.
/// </remarks>
[Route("organizations/{orgId}/users/keys")]
[Authorize("Application")]
public class OrganizationUsersKeysController : Controller
{
    private readonly IOrganizationUserKeyRepository _organizationUserKeyRepository;
    private readonly IApplyOrganizationUserV2UpgradesCommand _applyOrganizationUserV2UpgradesCommand;
    private readonly IOrganizationContext _organizationContext;

    public OrganizationUsersKeysController(
        IOrganizationUserKeyRepository organizationUserKeyRepository,
        IApplyOrganizationUserV2UpgradesCommand applyOrganizationUserV2UpgradesCommand,
        IOrganizationContext organizationContext)
    {
        _organizationUserKeyRepository = organizationUserKeyRepository;
        _applyOrganizationUserV2UpgradesCommand = applyOrganizationUserV2UpgradesCommand;
        _organizationContext = organizationContext;
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
    /// The client posts each page to <see cref="PostV2UpgradesAsync"/> and reads again, until the response is empty.
    /// </remarks>
    [HttpGet("pending-v2-upgrades")]
    [Authorize<OrganizationOwnerOrAdminRequirement>]
    public async Task<ListResponseModel<OrganizationUserPendingV2UpgradeResponseModel>> GetPendingV2UpgradesAsync(
        [FromRoute] Guid orgId)
    {
        var pending = await _organizationUserKeyRepository.GetManyPendingV2UpgradesByOrganizationIdAsync(
            orgId, IsOwner(orgId), OrganizationUserV2UpgradesRequestModel.MaxUpgrades);

        var responses = pending
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
    /// A membership that changed since the read, or that the caller cannot access, is skipped rather than rejected.
    /// </remarks>
    [HttpPost("v2-upgrades")]
    [Authorize<OrganizationOwnerOrAdminRequirement>]
    public async Task PostV2UpgradesAsync([FromRoute] Guid orgId,
        [FromBody] OrganizationUserV2UpgradesRequestModel model)
    {
        await _applyOrganizationUserV2UpgradesCommand.ApplyAsync(orgId, IsOwner(orgId), model.ToData());
    }

    /// <summary>
    /// Only an Owner can access the key material of another Owner.
    /// </summary>
    private bool IsOwner(Guid orgId) =>
        _organizationContext.GetOrganizationClaims(User, orgId)?.Type is OrganizationUserType.Owner;
}
