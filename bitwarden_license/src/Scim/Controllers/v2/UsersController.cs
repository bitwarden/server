// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Models;
using Bit.Scim.Users.Interfaces;
using Bit.Scim.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IRevokeOrganizationUserCommand = Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v1.IRevokeOrganizationUserCommand;
using IRevokeOrganizationUserCommandV2 = Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2.IRevokeOrganizationUserCommand;

namespace Bit.Scim.Controllers.v2;

[Authorize("Scim")]
[Route("v2/{organizationId}/users")]
[Produces("application/scim+json")]
[ExceptionHandlerFilter]
public class UsersController : Controller
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IGetUsersListQuery _getUsersListQuery;
    private readonly IRemoveOrganizationUserCommand _removeOrganizationUserCommand;
    private readonly IPatchUserCommand _patchUserCommand;
    private readonly IPostUserCommand _postUserCommand;
    private readonly IRestoreOrganizationUserCommand _restoreOrganizationUserCommand;
    private readonly IRevokeOrganizationUserCommand _revokeOrganizationUserCommand;
    private readonly IFeatureService _featureService;
    private readonly IRevokeOrganizationUserCommandV2 _revokeOrganizationUserCommandV2;

    public UsersController(IOrganizationUserRepository organizationUserRepository,
        IGetUsersListQuery getUsersListQuery,
        IRemoveOrganizationUserCommand removeOrganizationUserCommand,
        IPatchUserCommand patchUserCommand,
        IPostUserCommand postUserCommand,
        IRestoreOrganizationUserCommand restoreOrganizationUserCommand,
        IRevokeOrganizationUserCommand revokeOrganizationUserCommand,
        IFeatureService featureService,
        IRevokeOrganizationUserCommandV2 revokeOrganizationUserCommandV2)
    {
        _organizationUserRepository = organizationUserRepository;
        _getUsersListQuery = getUsersListQuery;
        _removeOrganizationUserCommand = removeOrganizationUserCommand;
        _patchUserCommand = patchUserCommand;
        _postUserCommand = postUserCommand;
        _restoreOrganizationUserCommand = restoreOrganizationUserCommand;
        _revokeOrganizationUserCommand = revokeOrganizationUserCommand;
        _featureService = featureService;
        _revokeOrganizationUserCommandV2 = revokeOrganizationUserCommandV2;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid organizationId, Guid id)
    {
        var orgUser = await _organizationUserRepository.GetDetailsByIdAsync(id);
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            throw new NotFoundException("User not found.");
        }
        return Ok(new ScimUserResponseModel(orgUser));
    }

    [HttpGet("")]
    public async Task<IActionResult> Get(
        Guid organizationId,
        [FromQuery] GetUsersQueryParamModel model)
    {
        var usersListQueryResult = await _getUsersListQuery.GetUsersListAsync(organizationId, model);
        var scimListResponseModel = new ScimListResponseModel<ScimUserResponseModel>
        {
            Resources = usersListQueryResult.userList.Select(u => new ScimUserResponseModel(u)).ToList(),
            ItemsPerPage = model.Count,
            TotalResults = usersListQueryResult.totalResults,
            StartIndex = model.StartIndex,
        };
        return Ok(scimListResponseModel);
    }

    [HttpPost("")]
    public async Task<IActionResult> Post(Guid organizationId, [FromBody] ScimUserRequestModel model)
    {
        var orgUser = await _postUserCommand.PostUserAsync(organizationId, model);
        var scimUserResponseModel = new ScimUserResponseModel(orgUser);
        return new CreatedResult(Url.Action(nameof(Get), new { orgUser.OrganizationId, orgUser.Id }), scimUserResponseModel);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(Guid organizationId, Guid id, [FromBody] ScimUserRequestModel model)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(id);
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            return new NotFoundObjectResult(new ScimErrorResponseModel
            {
                Status = 404,
                Detail = "User not found."
            });
        }

        if (model.Active && orgUser.Status == OrganizationUserStatusType.Revoked)
        {
            await _restoreOrganizationUserCommand.RestoreUserAsync(orgUser, EventSystemUser.SCIM);
        }
        else if (!model.Active && orgUser.Status != OrganizationUserStatusType.Revoked)
        {
            if (_featureService.IsEnabled(FeatureFlagKeys.ScimRevokeV2))
            {
                var results = await _revokeOrganizationUserCommandV2.RevokeUsersAsync(
                    new RevokeOrganizationUsersRequest(
                        organizationId,
                        [id],
                        new SystemUser(EventSystemUser.SCIM)));

                var errors = results.Select(x => x.Result.Match(
                    y => $"{y.Message} for user {x.Id}",
                    _ => null))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (errors.Count != 0)
                {
                    return new BadRequestObjectResult(new ScimErrorResponseModel
                    {
                        Status = 400,
                        Detail = string.Join(", ", errors)
                    });
                }
            }
            else
            {
                await _revokeOrganizationUserCommand.RevokeUserAsync(orgUser, EventSystemUser.SCIM);
            }
        }

        // Have to get full details object for response model
        var orgUserDetails = await _organizationUserRepository.GetDetailsByIdAsync(id);
        return new ObjectResult(new ScimUserResponseModel(orgUserDetails));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(Guid organizationId, Guid id, [FromBody] ScimPatchModel model)
    {
        await _patchUserCommand.PatchUserAsync(organizationId, id, model);
        return new NoContentResult();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid organizationId, Guid id)
    {
        await _removeOrganizationUserCommand.RemoveUserAsync(organizationId, id, EventSystemUser.SCIM);
        return new NoContentResult();
    }
}
