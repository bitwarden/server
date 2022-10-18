using Bit.Core.Enums;
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
    private readonly IGetUserQuery _getUserQuery;
    private readonly IDeleteOrganizationUserCommand _deleteOrganizationUserCommand;
    private readonly IPatchUserCommand _patchUserCommand;
    private readonly IPostUserCommand _postUserCommand;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService,
        IGetUserQuery getUserQuery,
        IDeleteOrganizationUserCommand deleteOrganizationUserCommand,
        IPatchUserCommand patchUserCommand,
        IPostUserCommand postUserCommand,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
        _getUserQuery = getUserQuery;
        _deleteOrganizationUserCommand = deleteOrganizationUserCommand;
        _patchUserCommand = patchUserCommand;
        _postUserCommand = postUserCommand;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid organizationId, Guid id)
    {
        var scimUserResponseModel = await _getUserQuery.GetUserAsync(organizationId, id);
        return Ok(scimUserResponseModel);
    }

    [HttpGet("")]
    public async Task<IActionResult> Get(
        Guid organizationId,
        [FromQuery] string filter,
        [FromQuery] int? count,
        [FromQuery] int? startIndex)
    {
        string emailFilter = null;
        string usernameFilter = null;
        string externalIdFilter = null;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            if (filter.StartsWith("userName eq "))
            {
                usernameFilter = filter.Substring(12).Trim('"').ToLowerInvariant();
                if (usernameFilter.Contains("@"))
                {
                    emailFilter = usernameFilter;
                }
            }
            else if (filter.StartsWith("externalId eq "))
            {
                externalIdFilter = filter.Substring(14).Trim('"');
            }
        }

        var userList = new List<ScimUserResponseModel> { };
        var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);
        var totalResults = 0;
        if (!string.IsNullOrWhiteSpace(emailFilter))
        {
            var orgUser = orgUsers.FirstOrDefault(ou => ou.Email.ToLowerInvariant() == emailFilter);
            if (orgUser != null)
            {
                userList.Add(new ScimUserResponseModel(orgUser));
            }
            totalResults = userList.Count;
        }
        else if (!string.IsNullOrWhiteSpace(externalIdFilter))
        {
            var orgUser = orgUsers.FirstOrDefault(ou => ou.ExternalId == externalIdFilter);
            if (orgUser != null)
            {
                userList.Add(new ScimUserResponseModel(orgUser));
            }
            totalResults = userList.Count;
        }
        else if (string.IsNullOrWhiteSpace(filter) && startIndex.HasValue && count.HasValue)
        {
            userList = orgUsers.OrderBy(ou => ou.Email)
                .Skip(startIndex.Value - 1)
                .Take(count.Value)
                .Select(ou => new ScimUserResponseModel(ou))
                .ToList();
            totalResults = orgUsers.Count;
        }

        var result = new ScimListResponseModel<ScimUserResponseModel>
        {
            Resources = userList,
            ItemsPerPage = count.GetValueOrDefault(userList.Count),
            TotalResults = totalResults,
            StartIndex = startIndex.GetValueOrDefault(1),
        };
        return new ObjectResult(result);
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
        await _deleteOrganizationUserCommand.DeleteUserAsync(organizationId, id, null);
        return new NoContentResult();
    }
}
