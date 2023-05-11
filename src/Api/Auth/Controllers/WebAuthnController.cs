using Amazon.Runtime.Credentials.Internal;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Request.Webauthn;
using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Api.Auth.Models.Response.WebAuthn;
using Bit.Api.Models.Response;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Auth.Controllers;

[Route("webauthn")]
[Authorize("Web")]
public class WebAuthnController : Controller
{
    private readonly IUserService _userService;
    private readonly IWebAuthnCredentialRepository _credentialRepository;
    private readonly IDataProtectorTokenFactory<WebAuthnCredentialCreateOptionsTokenable> _createOptionsDataProtector;

    public WebAuthnController(
        IUserService userService,
        IWebAuthnCredentialRepository credentialRepository,
        IDataProtectorTokenFactory<WebAuthnCredentialCreateOptionsTokenable> createOptionsDataProtector)
    {
        _userService = userService;
        _credentialRepository = credentialRepository;
        _createOptionsDataProtector = createOptionsDataProtector;
    }

    [HttpGet("")]
    // TODO: Create proper models for this call
    public async Task<ListResponseModel<WebAuthnCredentialResponseModel>> Get()
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var credentials = await _credentialRepository.GetManyByUserIdAsync(user.Id);

        return new ListResponseModel<WebAuthnCredentialResponseModel>(credentials.Select(c => new WebAuthnCredentialResponseModel(c)));
    }

    [HttpPost("options")]
    [ApiExplorerSettings(IgnoreApi = true)] // Disable Swagger due to CredentialCreateOptions not converting properly
    public async Task<WebAuthnCredentialCreateOptionsResponseModel> PostOptions([FromBody] SecretVerificationRequestModel model)
    {
        var user = await CheckAsync(model);
        var options = await _userService.StartWebAuthnLoginRegistrationAsync(user);

        var tokenable = new WebAuthnCredentialCreateOptionsTokenable(user, options);
        var token = _createOptionsDataProtector.Protect(tokenable);

        return new WebAuthnCredentialCreateOptionsResponseModel
        {
            Options = options,
            Token = token,
        };
    }

    [HttpPost("")]
    // TODO: Create proper models for this call
    public async Task<TwoFactorWebAuthnResponseModel> Post([FromBody] WebAuthnCredentialRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);

        var tokenable = _createOptionsDataProtector.Unprotect(model.Token);
        if (!tokenable.TokenIsValid(user))
        {
            throw new BadRequestException("The token associated with your request is expired. A valid token is required to continue.");
        }

        var success = await _userService.CompleteWebAuthLoginRegistrationAsync(user, model.Name, tokenable.Options, model.DeviceResponse);
        if (!success)
        {
            throw new BadRequestException("Unable to complete WebAuthn registration.");
        }
        var response = new TwoFactorWebAuthnResponseModel(user);
        return response;
    }

    [HttpPost("{id}/delete")]
    public async Task Delete(string id, [FromBody] SecretVerificationRequestModel model)
    {
        var user = await CheckAsync(model);
        var credential = await _credentialRepository.GetByIdAsync(new Guid(id), user.Id);
        if (credential == null)
        {
            throw new NotFoundException("Credential not found.");
        }

        await _credentialRepository.DeleteAsync(credential);
    }

    private async Task<Core.Entities.User> CheckAsync(SecretVerificationRequestModel model)
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
