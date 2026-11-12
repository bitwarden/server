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
        [FromQuery] int groupCount = int.MaxValue)
    {
        var projection = new ScimAttributeProjection(Request);
        var orgUser = await _organizationUserRepository.GetDetailsByIdAsync(id);
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            throw new NotFoundException("User not found.");
        }

        var userResponse = new ScimUserResponseModel(orgUser);

        // Populate groups for this user with pagination
        var groupIds = projection.Includes("groups")
            ? await _organizationUserRepository.GetManyGroupIdsByUserIdAsync(orgUser.Id)
            : Array.Empty<Guid>();
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

        return Ok(projection.Apply(userResponse));
    }

    [HttpGet("")]
    public async Task<IActionResult> Get(
        [FromRoute] Guid organizationId,
        [FromQuery] GetUsersQueryParamModel model)
    {
        var projection = new ScimAttributeProjection(Request);
        var usersListQueryResult = await _getUsersListQuery.GetUsersListAsync(organizationId, model);

        var users = usersListQueryResult.userList.ToList();
        var groupIdsByUser = new Dictionary<Guid, List<Guid>>();
        var groupsById = new Dictionary<Guid, Group>();
        if (projection.Includes("groups") && users.Count > 0)
        {
            var memberships = await _groupRepository.GetManyGroupUsersByOrganizationUserIdsAsync(
                organizationId, users.Select(user => user.Id));
            groupIdsByUser = memberships.GroupBy(membership => membership.OrganizationUserId)
                .ToDictionary(user => user.Key, user => user.Select(membership => membership.GroupId)
                    .OrderBy(id => id).Skip(model.GroupStartIndex - 1).Take(model.GroupCount).ToList());
            var groupIds = groupIdsByUser.Values.SelectMany(ids => ids).Distinct().ToList();
            if (groupIds.Count > 0)
            {
                groupsById = (await _groupRepository.GetManyByManyIds(groupIds))
                    .Where(group => group.OrganizationId == organizationId).ToDictionary(group => group.Id);
            }
        }

        var userResponses = new List<ScimUserResponseModel>();
        foreach (var user in users)
        {
            var userResponse = new ScimUserResponseModel(user);

            if (groupIdsByUser.TryGetValue(user.Id, out var groupIds))
            {
                userResponse.Groups = ToGroupReferences(organizationId,
                    groupIds.Where(groupsById.ContainsKey).Select(id => groupsById[id]));
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
        return Ok(projection.Apply(scimListResponseModel, list: true));
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
