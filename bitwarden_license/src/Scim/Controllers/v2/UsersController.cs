using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Models;
using Bit.Scim.Users.Interfaces;
using Bit.Scim.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Scim.Controllers.v2;

[Authorize("Scim")]
[Route("v2/{organizationId}/users")]
[ExceptionHandlerFilter]
public class UsersController : Controller
{
    private readonly IUserService _userService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IGetUsersListQuery _getUsersListQuery;
    private readonly IDeleteOrganizationUserCommand _deleteOrganizationUserCommand;
    private readonly IPatchUserCommand _patchUserCommand;
    private readonly IPostUserCommand _postUserCommand;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService,
        IGetUsersListQuery getUsersListQuery,
        IDeleteOrganizationUserCommand deleteOrganizationUserCommand,
        IPatchUserCommand patchUserCommand,
        IPostUserCommand postUserCommand,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
        _getUsersListQuery = getUsersListQuery;
        _deleteOrganizationUserCommand = deleteOrganizationUserCommand;
        _patchUserCommand = patchUserCommand;
        _postUserCommand = postUserCommand;
        _logger = logger;
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
        [FromQuery] string filter,
        [FromQuery] int? count,
        [FromQuery] int? startIndex)
    {
        var usersListQueryResult = await _getUsersListQuery.GetUsersListAsync(organizationId, filter, count, startIndex);
        var scimListResponseModel = new ScimListResponseModel<ScimUserResponseModel>
        {
            Resources = usersListQueryResult.userList.Select(u => new ScimUserResponseModel(u)).ToList(),
            ItemsPerPage = count.GetValueOrDefault(usersListQueryResult.userList.Count()),
            TotalResults = usersListQueryResult.totalResults,
            StartIndex = startIndex.GetValueOrDefault(1),
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
            await _organizationService.RestoreUserAsync(orgUser, null, _userService);
        }
        else if (!model.Active && orgUser.Status != OrganizationUserStatusType.Revoked)
        {
            await _organizationService.RevokeUserAsync(orgUser, null);
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
        await _deleteOrganizationUserCommand.DeleteUserAsync(organizationId, id, EventSystemUser.SCIM);
        return new NoContentResult();
    }
}
