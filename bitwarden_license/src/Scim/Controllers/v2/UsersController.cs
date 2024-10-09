using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
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
[Produces("application/scim+json")]
[ExceptionHandlerFilter]
public class UsersController : Controller
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IGetUsersListQuery _getUsersListQuery;
    private readonly IRemoveOrganizationUserCommand _removeOrganizationUserCommand;
    private readonly IPatchUserCommand _patchUserCommand;
    private readonly IPostUserCommand _postUserCommand;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService,
        IGetUsersListQuery getUsersListQuery,
        IRemoveOrganizationUserCommand removeOrganizationUserCommand,
        IPatchUserCommand patchUserCommand,
        IPostUserCommand postUserCommand,
        ILogger<UsersController> logger)
    {
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
        _getUsersListQuery = getUsersListQuery;
        _removeOrganizationUserCommand = removeOrganizationUserCommand;
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
            await _organizationService.RestoreUserAsync(orgUser, EventSystemUser.SCIM);
        }
        else if (!model.Active && orgUser.Status != OrganizationUserStatusType.Revoked)
        {
            await _organizationService.RevokeUserAsync(orgUser, EventSystemUser.SCIM);
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
