using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Api.Models.Response;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Fido2NetLib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Auth.Controllers;

[Route("webauthn")]
[Authorize("Web")]
public class WebAuthnController : Controller
{
    private readonly IUserService _userService;

    public WebAuthnController(
        IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("")]
    // TODO: Create proper models for this call
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
        var user = await CheckAsync(model);
        var reg = await _userService.StartWebAuthnLoginRegistrationAsync(user);
        return reg;
    }

    [HttpPost("")]
    // TODO: Create proper models for this call
    public async Task<TwoFactorWebAuthnResponseModel> Post([FromBody] TwoFactorWebAuthnRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var success = await _userService.CompleteWebAuthLoginRegistrationAsync(user, model.Name, model.DeviceResponse);
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
        // TODO: Delete
    }

    private async Task<User> CheckAsync(SecretVerificationRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        if (!await _userService.VerifySecretAsync(user, model.Secret))
        {
            await Task.Delay(2000);
            throw new BadRequestException(string.Empty, "User verification failed.");
        }

        // TODO: Is premium requried?
        //if (premium && !(await _userService.CanAccessPremium(user)))
        //{
        //    throw new BadRequestException("Premium status is required.");
        //}

        return user;
    }
}
