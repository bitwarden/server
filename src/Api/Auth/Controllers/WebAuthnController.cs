using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Request.Webauthn;
using Bit.Api.Auth.Models.Response.WebAuthn;
using Bit.Api.Models.Response;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Microsoft.AspNetCore.Authorization;
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
    public async Task<ListResponseModel<WebAuthnCredentialResponseModel>> Get()
    {
        var user = await GetUser();
        var credentials = await _credentialRepository.GetManyByUserIdAsync(user.Id);

        return new ListResponseModel<WebAuthnCredentialResponseModel>(credentials.Select(c => new WebAuthnCredentialResponseModel(c)));
    }

    [HttpPost("options")]
    public async Task<WebAuthnCredentialCreateOptionsResponseModel> PostOptions([FromBody] SecretVerificationRequestModel model)
    {
        var user = await VerifyUser(model);
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
    public async Task Post([FromBody] WebAuthnCredentialRequestModel model)
    {
        var user = await GetUser();
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
    }

    [HttpPost("{id}/delete")]
    public async Task Delete(Guid id, [FromBody] SecretVerificationRequestModel model)
    {
        var user = await VerifyUser(model);
        var credential = await _credentialRepository.GetByIdAsync(id, user.Id);
        if (credential == null)
        {
            throw new NotFoundException("Credential not found.");
        }

        await _credentialRepository.DeleteAsync(credential);
    }

    private async Task<Core.Entities.User> GetUser()
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }
        return user;
    }

    private async Task<Core.Entities.User> VerifyUser(SecretVerificationRequestModel model)
    {
        var user = await GetUser();
        if (!await _userService.VerifySecretAsync(user, model.Secret))
        {
            await Task.Delay(2000);
            throw new BadRequestException(string.Empty, "User verification failed.");
        }

        return user;
    }
}
