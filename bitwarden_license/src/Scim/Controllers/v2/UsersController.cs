using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Commands.Users.Interfaces;
using Bit.Scim.Context;
using Bit.Scim.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bit.Scim.Controllers.v2;

[Authorize("Scim")]
[Route("v2/{organizationId}/users")]
public class UsersController : Controller
{
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IScimContext _scimContext;
    private readonly ScimSettings _scimSettings;
    private readonly IGetUserCommand _getUserCommand;
    private readonly IGetUsersListCommand _getUsersListCommand;
    private readonly IPostUserCommand _postUserCommand;
    private readonly IPutUserCommand _putUserCommand;
    private readonly IPatchUserCommand _patchUserCommand;
    private readonly IDeleteUserCommand _deleteUserCommand;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService,
        IScimContext scimContext,
        IOptions<ScimSettings> scimSettings,
        IGetUserCommand getUserCommand,
        IGetUsersListCommand getUsersListCommand,
        IPostUserCommand postUserCommand,
        IPutUserCommand putUserCommand,
        IPatchUserCommand patchUserCommand,
        IDeleteUserCommand deleteUserCommand,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _userRepository = userRepository;
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
        _scimContext = scimContext;
        _scimSettings = scimSettings?.Value;
        _getUserCommand = getUserCommand;
        _getUsersListCommand = getUsersListCommand;
        _postUserCommand = postUserCommand;
        _putUserCommand = putUserCommand;
        _patchUserCommand = patchUserCommand;
        _deleteUserCommand = deleteUserCommand;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid organizationId, Guid id)
    {
        try
        {
            var scimUserResponseModel = await _getUserCommand.GetUserAsync(organizationId, id);
            return Ok(scimUserResponseModel);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ScimErrorResponseModel
            {
                Status = StatusCodes.Status404NotFound,
                Detail = ex.Message
            });
        }
    }

    [HttpGet("")]
    public async Task<IActionResult> Get(
        Guid organizationId,
        [FromQuery] string filter,
        [FromQuery] int? count,
        [FromQuery] int? startIndex)
    {
        var scimListResponseModel = await _getUsersListCommand.GetUsersListAsync(organizationId, filter, count, startIndex);
        return Ok(scimListResponseModel);
    }

    [HttpPost("")]
    public async Task<IActionResult> Post(Guid organizationId, [FromBody] ScimUserRequestModel model)
    {
        try
        {
            var orgUser = await _postUserCommand.PostUserAsync(organizationId, model);
            var scimUserResponseModel = new ScimUserResponseModel(orgUser);
            return new CreatedResult(Url.Action(nameof(Get), new { orgUser.OrganizationId, orgUser.Id }), scimUserResponseModel);

        }
        catch (BadRequestException)
        {
            return BadRequest();
        }
        catch (ConflictException)
        {
            return Conflict();
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(Guid organizationId, Guid id, [FromBody] ScimUserRequestModel model)
    {
        try
        {
            var scimUserResponseModel = await _putUserCommand.PutUserAsync(organizationId, id, model);
            return Ok(scimUserResponseModel);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ScimErrorResponseModel
            {
                Status = StatusCodes.Status404NotFound,
                Detail = ex.Message
            });
        }
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(Guid organizationId, Guid id, [FromBody] ScimPatchModel model)
    {
        try
        {
            await _patchUserCommand.PatchUserAsync(organizationId, id, model);
            return new NoContentResult();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ScimErrorResponseModel
            {
                Status = StatusCodes.Status404NotFound,
                Detail = ex.Message
            });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid organizationId, Guid id, [FromBody] ScimUserRequestModel model)
    {
        try
        {
            await _deleteUserCommand.DeleteUserAsync(organizationId, id, model);
            return new NoContentResult();
        }
        catch (NotFoundException)
        {
            return NotFound(new ScimErrorResponseModel
            {
                Status = StatusCodes.Status404NotFound,
                Detail = "User not found."
            });
        }
    }
}
