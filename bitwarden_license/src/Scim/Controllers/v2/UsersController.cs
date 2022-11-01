using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Scim.Context;
using Bit.Scim.Models;
using Bit.Scim.Queries.Users.Interfaces;
using Bit.Scim.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bit.Scim.Controllers.v2;

[Authorize("Scim")]
[Route("v2/{organizationId}/users")]
[ExceptionHandlerFilter]
public class UsersController : Controller
{
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IScimContext _scimContext;
    private readonly ScimSettings _scimSettings;
    private readonly IGetUserQuery _getUserQuery;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService,
        IScimContext scimContext,
        IOptions<ScimSettings> scimSettings,
        IGetUserQuery getUserQuery,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _userRepository = userRepository;
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
        _scimContext = scimContext;
        _scimSettings = scimSettings?.Value;
        _getUserQuery = getUserQuery;
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
        var email = model.PrimaryEmail?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            switch (_scimContext.RequestScimProvider)
            {
                case ScimProviderType.AzureAd:
                    email = model.UserName?.ToLowerInvariant();
                    break;
                default:
                    email = model.WorkEmail?.ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        email = model.Emails?.FirstOrDefault()?.Value?.ToLowerInvariant();
                    }
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(email) || !model.Active)
        {
            return new BadRequestResult();
        }

        var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);
        var orgUserByEmail = orgUsers.FirstOrDefault(ou => ou.Email?.ToLowerInvariant() == email);
        if (orgUserByEmail != null)
        {
            return new ConflictResult();
        }

        string externalId = null;
        if (!string.IsNullOrWhiteSpace(model.ExternalId))
        {
            externalId = model.ExternalId;
        }
        else if (!string.IsNullOrWhiteSpace(model.UserName))
        {
            externalId = model.UserName;
        }
        else
        {
            externalId = CoreHelpers.RandomString(15);
        }

        var orgUserByExternalId = orgUsers.FirstOrDefault(ou => ou.ExternalId == externalId);
        if (orgUserByExternalId != null)
        {
            return new ConflictResult();
        }

        var invitedOrgUser = await _organizationService.InviteUserAsync(organizationId, null, email,
            OrganizationUserType.User, false, externalId, new List<CollectionAccessSelection>());
        var orgUser = await _organizationUserRepository.GetDetailsByIdAsync(invitedOrgUser.Id);
        var response = new ScimUserResponseModel(orgUser);
        return new CreatedResult(Url.Action(nameof(Get), new { orgUser.OrganizationId, orgUser.Id }), response);
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
        var orgUser = await _organizationUserRepository.GetByIdAsync(id);
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            return new NotFoundObjectResult(new ScimErrorResponseModel
            {
                Status = 404,
                Detail = "User not found."
            });
        }

        var operationHandled = false;
        foreach (var operation in model.Operations)
        {
            // Replace operations
            if (operation.Op?.ToLowerInvariant() == "replace")
            {
                // Active from path
                if (operation.Path?.ToLowerInvariant() == "active")
                {
                    var active = operation.Value.ToString()?.ToLowerInvariant();
                    var handled = await HandleActiveOperationAsync(orgUser, active == "true");
                    if (!operationHandled)
                    {
                        operationHandled = handled;
                    }
                }
                // Active from value object
                else if (string.IsNullOrWhiteSpace(operation.Path) &&
                    operation.Value.TryGetProperty("active", out var activeProperty))
                {
                    var handled = await HandleActiveOperationAsync(orgUser, activeProperty.GetBoolean());
                    if (!operationHandled)
                    {
                        operationHandled = handled;
                    }
                }
            }
        }

        if (!operationHandled)
        {
            _logger.LogWarning("User patch operation not handled: {operation} : ",
                string.Join(", ", model.Operations.Select(o => $"{o.Op}:{o.Path}")));
        }

        return new NoContentResult();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid organizationId, Guid id)
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
        await _organizationService.DeleteUserAsync(organizationId, id, null);
        return new NoContentResult();
    }

    private async Task<bool> HandleActiveOperationAsync(Core.Entities.OrganizationUser orgUser, bool active)
    {
        if (active && orgUser.Status == OrganizationUserStatusType.Revoked)
        {
            await _organizationService.RestoreUserAsync(orgUser, null, _userService);
            return true;
        }
        else if (!active && orgUser.Status != OrganizationUserStatusType.Revoked)
        {
            await _organizationService.RevokeUserAsync(orgUser, null);
            return true;
        }
        return false;
    }
}
