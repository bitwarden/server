﻿using Bit.Api.Auth.Models.Request.Opaque;
using Bit.Api.Auth.Models.Response.Opaque;
using Bit.Core;
using Bit.Core.Auth.Models.Api.Request.Opaque;
using Bit.Core.Auth.Models.Api.Response.Opaque;
using Bit.Core.Auth.Services;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Auth.Controllers;

// TODO: move to identity
[Route("opaque")]
[RequireFeature(FeatureFlagKeys.OpaqueKeyExchange)]
public class OpaqueKeyExchangeController : Controller
{
    private readonly IOpaqueKeyExchangeService _opaqueKeyExchangeService;
    private readonly IUserService _userService;

    public OpaqueKeyExchangeController(
        IOpaqueKeyExchangeService opaqueKeyExchangeService,
        IUserService userService
    )
    {
        _opaqueKeyExchangeService = opaqueKeyExchangeService;
        _userService = userService;
    }

    // TODO: investigate removing ~/opaque from all routes and using controller level route attribute
    [Authorize("Application")]
    [HttpPost("~/opaque/start-registration")]
    public async Task<OpaqueRegistrationStartResponse> StartRegistrationAsync([FromBody] OpaqueRegistrationStartRequest request)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var result = await _opaqueKeyExchangeService.StartRegistration(Convert.FromBase64String(request.RegistrationRequest), user, request.CipherConfiguration);
        return result;
    }


    [Authorize("Application")]
    [HttpPost("~/opaque/finish-registration")]
    public async void FinishRegistrationAsync([FromBody] OpaqueRegistrationFinishRequest request)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        await _opaqueKeyExchangeService.FinishRegistration(request.SessionId, Convert.FromBase64String(request.RegistrationUpload), user, request.KeySet);
    }

    [Authorize("Application")]
    [HttpPost("~/opaque/set-registration-active")]
    public async void SetRegistrationActive([FromBody] OpaqueSetRegistrationActiveRequest request)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        await _opaqueKeyExchangeService.SetRegistrationActiveForAccount(request.SessionId, user);
    }

    // TODO: Remove and move to token endpoint
    [AllowAnonymous]
    [HttpPost("~/opaque/start-login")]
    public async Task<OpaqueLoginStartResponse> StartLoginAsync([FromBody] OpaqueLoginStartRequest request)
    {
        var result = await _opaqueKeyExchangeService.StartLogin(Convert.FromBase64String(request.CredentialRequest), request.Email);
        return new OpaqueLoginStartResponse(result.Item1, Convert.ToBase64String(result.Item2));
    }

    // TODO: Remove and move to token endpoint
    [AllowAnonymous]
    [HttpPost("~/opaque/finish-login")]
    public async Task<bool> FinishLoginAsync([FromBody] OpaqueLoginFinishRequest request)
    {
        var result = await _opaqueKeyExchangeService.FinishLogin(request.SessionId, Convert.FromBase64String(request.CredentialFinalization));
        return result;
    }
}
