// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Scim.Models;
using Bit.Scim.Users.Interfaces;
using Bit.Scim.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IRevokeOrganizationUserCommandV2 = Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2.IRevokeOrganizationUserCommand;

namespace Bit.Scim.Controllers.v2;

[Authorize("Scim")]
[Route("v2/{organizationId}/users")]
[Produces("application/scim+json")]
[ExceptionHandlerFilter]
public class UsersController : Controller
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IGetUsersListQuery _getUsersListQuery;
    private readonly IRemoveOrganizationUserCommand _removeOrganizationUserCommand;
    private readonly IPatchUserCommand _patchUserCommand;
    private readonly IPostUserCommand _postUserCommand;
    private readonly IRestoreOrganizationUserCommand _restoreOrganizationUserCommand;
    private readonly IRevokeOrganizationUserCommandV2 _revokeOrganizationUserCommandV2;

    public UsersController(IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        IGetUsersListQuery getUsersListQuery,
        IRemoveOrganizationUserCommand removeOrganizationUserCommand,
        IPatchUserCommand patchUserCommand,
        IPostUserCommand postUserCommand,
        IRestoreOrganizationUserCommand restoreOrganizationUserCommand,
        IRevokeOrganizationUserCommandV2 revokeOrganizationUserCommandV2)
    {
        _organizationUserRepository = organizationUserRepository;
        _groupRepository = groupRepository;
        _getUsersListQuery = getUsersListQuery;
        _removeOrganizationUserCommand = removeOrganizationUserCommand;
        _patchUserCommand = patchUserCommand;
        _postUserCommand = postUserCommand;
        _restoreOrganizationUserCommand = restoreOrganizationUserCommand;
        _revokeOrganizationUserCommandV2 = revokeOrganizationUserCommandV2;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(
        [FromRoute] Guid organizationId,
        [FromRoute] Guid id,
        [FromQuery] int groupStartIndex = 1,
        [FromQuery] int groupCount = 50)
    {
        var orgUser = await _organizationUserRepository.GetDetailsByIdAsync(id);
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            throw new NotFoundException("User not found.");
        }

        var userResponse = new ScimUserResponseModel(orgUser);

        // Populate groups for this user with pagination
        var groupIds = await _organizationUserRepository.GetManyGroupIdsByUserIdAsync(orgUser.Id);
        if (groupIds.Any())
        {
            var groups = await _groupRepository.GetManyByManyIds(groupIds);

            // Apply pagination to groups
            var paginatedGroupIds = groupIds
                .OrderBy(guid => guid) // Ensure deterministic ordering
                .Skip(groupStartIndex - 1)
                .Take(groupCount)
                .ToList();

            var paginatedGroups = groups.Where(g => paginatedGroupIds.Contains(g.Id));
            userResponse.Groups = ToGroupReferences(organizationId, paginatedGroups);
        }

        return Ok(userResponse);
    }

    [HttpGet("")]
    public async Task<IActionResult> Get(
        [FromRoute] Guid organizationId,
        [FromQuery] GetUsersQueryParamModel model)
    {
        var usersListQueryResult = await _getUsersListQuery.GetUsersListAsync(organizationId, model);

        // Populate groups for each user
        var userResponses = new List<ScimUserResponseModel>();
        foreach (var user in usersListQueryResult.userList)
        {
            var userResponse = new ScimUserResponseModel(user);

            // Fetch group IDs for this user
            var groupIds = await _organizationUserRepository.GetManyGroupIdsByUserIdAsync(user.Id);

            if (groupIds.Any())
            {
                // Fetch group details
                var groups = await _groupRepository.GetManyByManyIds(groupIds);

                // Apply pagination to groups
                var paginatedGroupIds = groupIds
                    .OrderBy(id => id) // Ensure deterministic ordering
                    .Skip(model.GroupStartIndex - 1)
                    .Take(model.GroupCount)
                    .ToList();

                var paginatedGroups = groups.Where(g => paginatedGroupIds.Contains(g.Id));

                userResponse.Groups = ToGroupReferences(organizationId, paginatedGroups);
            }

            userResponses.Add(userResponse);
        }

        var scimListResponseModel = new ScimListResponseModel<ScimUserResponseModel>
        {
            Resources = userResponses,
            ItemsPerPage = model.Count,
            TotalResults = usersListQueryResult.totalResults,
            StartIndex = model.StartIndex,
        };
        return Ok(scimListResponseModel);
    }

    [HttpPost("")]
    public async Task<IActionResult> Post([FromRoute] Guid organizationId, [FromBody] ScimUserRequestModel model)
    {
        var orgUser = await _postUserCommand.PostUserAsync(organizationId, model);
        var scimUserResponseModel = new ScimUserResponseModel(orgUser);
        return new CreatedResult(Url.Action(nameof(Get), new { orgUser.OrganizationId, orgUser.Id }), scimUserResponseModel);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put([FromRoute] Guid organizationId, Guid id, [FromBody] ScimUserRequestModel model)
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
            var results = await _revokeOrganizationUserCommandV2.RevokeUsersAsync(
                new RevokeOrganizationUsersRequest(
                    organizationId,
                    [id],
                    new SystemUser(EventSystemUser.SCIM),
                    RevocationReason.Manual));

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

        // Have to get full details object for response model
        var orgUserDetails = await _organizationUserRepository.GetDetailsByIdAsync(id);
        return new ObjectResult(new ScimUserResponseModel(orgUserDetails));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch([FromRoute] Guid organizationId, Guid id, [FromBody] ScimPatchModel model)
    {
        await _patchUserCommand.PatchUserAsync(organizationId, id, model);
        return new NoContentResult();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete([FromRoute] Guid organizationId, Guid id)
    {
        await _removeOrganizationUserCommand.RemoveUserAsync(organizationId, id, EventSystemUser.SCIM);
        return new NoContentResult();
    }

    private List<BaseScimUserModel.UserGroupReference> ToGroupReferences(Guid organizationId, IEnumerable<Group> groups)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/v2/{organizationId}/Groups";
        return groups
            .OrderBy(g => g.Id)
            .Select(g => new BaseScimUserModel.UserGroupReference
            {
                Value = g.Id.ToString(),
                Display = g.Name,
                Ref = $"{baseUrl}/{g.Id}",
                Type = "direct"
            })
            .ToList();
    }
}
