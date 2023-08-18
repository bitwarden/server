using Bit.Api.Auth.Models.Response;
using Bit.Api.Models.Response;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Auth.Services;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Auth.Controllers;

[Route("auth-requests")]
[Authorize("Application")]
public class AuthRequestsController : Controller
{
    private readonly IUserService _userService;
    private readonly IAuthRequestRepository _authRequestRepository;
    private readonly IGlobalSettings _globalSettings;
    private readonly IAuthRequestService _authRequestService;

    public AuthRequestsController(
        IUserService userService,
        IAuthRequestRepository authRequestRepository,
        IGlobalSettings globalSettings,
        IAuthRequestService authRequestService)
    {
        _userService = userService;
        _authRequestRepository = authRequestRepository;
        _globalSettings = globalSettings;
        _authRequestService = authRequestService;
    }

    [HttpGet("")]
    public async Task<ListResponseModel<AuthRequestResponseModel>> Get()
    {
        var userId = _userService.GetProperUserId(User).Value;
        var authRequests = await _authRequestRepository.GetManyByUserIdAsync(userId);
        var responses = authRequests.Select(a => new AuthRequestResponseModel(a, _globalSettings.BaseServiceUri.Vault)).ToList();
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
