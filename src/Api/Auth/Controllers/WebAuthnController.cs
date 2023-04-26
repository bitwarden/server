using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Api.Models.Request;
using Bit.Api.Models.Response;
using Bit.Api.Vault.Models.Response;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.LoginFeatures.PasswordlessLogin.Interfaces;
using Bit.Core.Auth.Utilities;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Fido2NetLib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Auth.Controllers;

[Route("webauthn")]
[Authorize("Web")]
public class WebAuthnController : Controller
{
    private readonly IUserService _userService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationService _organizationService;
    private readonly GlobalSettings _globalSettings;
    private readonly UserManager<User> _userManager;
    private readonly ICurrentContext _currentContext;
    private readonly IVerifyAuthRequestCommand _verifyAuthRequestCommand;

    public WebAuthnController(
        IUserService userService,
        IOrganizationRepository organizationRepository,
        IOrganizationService organizationService,
        GlobalSettings globalSettings,
        UserManager<User> userManager,
        ICurrentContext currentContext,
        IVerifyAuthRequestCommand verifyAuthRequestCommand)
    {
        _userService = userService;
        _organizationRepository = organizationRepository;
        _organizationService = organizationService;
        _globalSettings = globalSettings;
        _userManager = userManager;
        _currentContext = currentContext;
        _verifyAuthRequestCommand = verifyAuthRequestCommand;
    }

    [HttpGet("")]
    public async Task<ListResponseModel<TwoFactorWebAuthnResponseModel>> Get()
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }
        return new ListResponseModel<TwoFactorWebAuthnResponseModel>(new List<TwoFactorWebAuthnResponseModel> { });
    }

    [HttpPost("options")]
    [ApiExplorerSettings(IgnoreApi = true)] // Disable Swagger due to CredentialCreateOptions not converting properly
    public async Task<CredentialCreateOptions> PostOptions([FromBody] SecretVerificationRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var reg = await _userService.StartWebAuthnRegistrationAsync(user);
        return reg;
    }

    [HttpPost("")]
    public async Task<TwoFactorWebAuthnResponseModel> Post([FromBody] TwoFactorWebAuthnRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var success = await _userService.CompleteWebAuthRegistrationAsync(
            user, model.Id.Value, model.Name, model.DeviceResponse);
        if (!success)
        {
            throw new BadRequestException("Unable to complete WebAuthn registration.");
        }
        var response = new TwoFactorWebAuthnResponseModel(user);
        return response;
    }

    [HttpDelete("{id}")]
    [HttpPost("{id}/delete")]
    public async Task Delete(string id)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }
    }
}
