// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.Auth.Models.Response;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Auth.Services;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Auth.Controllers;

[Route("auth-requests")]
[Authorize("Application")]
public class AuthRequestsController(
    IUserService userService,
    IAuthRequestRepository authRequestRepository,
    IGlobalSettings globalSettings,
    IAuthRequestService authRequestService) : Controller
{
    private readonly IUserService _userService = userService;
    private readonly IAuthRequestRepository _authRequestRepository = authRequestRepository;
    private readonly IGlobalSettings _globalSettings = globalSettings;
    private readonly IAuthRequestService _authRequestService = authRequestService;

    [HttpGet("")]
    public async Task<ListResponseModel<AuthRequestResponseModel>> Get()
    {
        var userId = _userService.GetProperUserId(User).Value;
        var authRequests = await _authRequestRepository.GetManyByUserIdAsync(userId);
        var responses = authRequests.Select(a => new AuthRequestResponseModel(a, _globalSettings.BaseServiceUri.Vault));
        return new ListResponseModel<AuthRequestResponseModel>(responses);
    }

    [HttpGet("{id}")]
    public async Task<AuthRequestResponseModel> Get(Guid id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var authRequest = await _authRequestService.GetAuthRequestAsync(id, userId);

        if (authRequest == null)
        {
            throw new NotFoundException();
        }

        return new AuthRequestResponseModel(authRequest, _globalSettings.BaseServiceUri.Vault);
    }

    [HttpGet("pending")]
    [RequireFeature(FeatureFlagKeys.BrowserExtensionLoginApproval)]
    public async Task<ListResponseModel<PendingAuthRequestResponseModel>> GetPendingAuthRequestsAsync()
    {
        var userId = _userService.GetProperUserId(User).Value;
        var rawResponse = await _authRequestRepository.GetManyPendingAuthRequestByUserId(userId);
        var responses = rawResponse.Select(a => new PendingAuthRequestResponseModel(a, _globalSettings.BaseServiceUri.Vault));
        return new ListResponseModel<PendingAuthRequestResponseModel>(responses);
    }

    [HttpGet("{id}/response")]
    [AllowAnonymous]
    public async Task<AuthRequestResponseModel> GetResponse(Guid id, [FromQuery] string code)
    {
        var authRequest = await _authRequestService.GetValidatedAuthRequestAsync(id, code);

        if (authRequest == null)
        {
            throw new NotFoundException();
        }

        return new AuthRequestResponseModel(authRequest, _globalSettings.BaseServiceUri.Vault);
    }

    [HttpPost("")]
    [AllowAnonymous]
    public async Task<AuthRequestResponseModel> Post([FromBody] AuthRequestCreateRequestModel model)
    {
        if (model.Type == AuthRequestType.AdminApproval)
        {
            throw new BadRequestException("You must be authenticated to create a request of that type.");
        }
        var authRequest = await _authRequestService.CreateAuthRequestAsync(model);
        var r = new AuthRequestResponseModel(authRequest, _globalSettings.BaseServiceUri.Vault);
        return r;
    }

    [HttpPost("admin-request")]
    public async Task<AuthRequestResponseModel> PostAdminRequest([FromBody] AuthRequestCreateRequestModel model)
    {
        var authRequest = await _authRequestService.CreateAuthRequestAsync(model);
        var r = new AuthRequestResponseModel(authRequest, _globalSettings.BaseServiceUri.Vault);
        return r;
    }

    [HttpPut("{id}")]
    public async Task<AuthRequestResponseModel> Put(Guid id, [FromBody] AuthRequestUpdateRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var authRequest = await _authRequestService.UpdateAuthRequestAsync(id, userId, model);
        return new AuthRequestResponseModel(authRequest, _globalSettings.BaseServiceUri.Vault);
    }
}
