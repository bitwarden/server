using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Request.Webauthn;
using Bit.Api.Auth.Models.Response.WebAuthn;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Response.Accounts;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Auth.Controllers;

[Route("webauthn")]
[Authorize("Web")]
[RequireFeature(FeatureFlagKeys.PasswordlessLogin)]
public class WebAuthnController : Controller
{
    private readonly IUserService _userService;
    private readonly IWebAuthnCredentialRepository _credentialRepository;
    private readonly IDataProtectorTokenFactory<WebAuthnCredentialCreateOptionsTokenable> _createOptionsDataProtector;
    private readonly IPolicyService _policyService;
    private readonly IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable> _assertionOptionsDataProtector;

    public WebAuthnController(
        IUserService userService,
        IWebAuthnCredentialRepository credentialRepository,
        IDataProtectorTokenFactory<WebAuthnCredentialCreateOptionsTokenable> createOptionsDataProtector,
        IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable> assertionOptionsDataProtector,
        IPolicyService policyService)
    {
        _userService = userService;
        _credentialRepository = credentialRepository;
        _createOptionsDataProtector = createOptionsDataProtector;
        _assertionOptionsDataProtector = assertionOptionsDataProtector;
        _policyService = policyService;
    }

    [HttpGet("")]
    public async Task<ListResponseModel<WebAuthnCredentialResponseModel>> Get()
    {
        var user = await GetUserAsync();
        var credentials = await _credentialRepository.GetManyByUserIdAsync(user.Id);

        return new ListResponseModel<WebAuthnCredentialResponseModel>(credentials.Select(c => new WebAuthnCredentialResponseModel(c)));
    }

    [HttpPost("attestation-options")]
    public async Task<WebAuthnCredentialCreateOptionsResponseModel> AttestationOptions([FromBody] SecretVerificationRequestModel model)
    {
        var user = await VerifyUserAsync(model);
        await ValidateRequireSsoPolicyDisabledOrNotApplicable(user.Id);
        var options = await _userService.StartWebAuthnLoginRegistrationAsync(user);

        var tokenable = new WebAuthnCredentialCreateOptionsTokenable(user, options);
        var token = _createOptionsDataProtector.Protect(tokenable);

        return new WebAuthnCredentialCreateOptionsResponseModel
        {
            Options = options,
            Token = token
        };
    }

    [HttpPost("assertion-options")]
    public async Task<WebAuthnLoginAssertionOptionsResponseModel> AssertionOptions([FromBody] SecretVerificationRequestModel model)
    {
        var user = await VerifyUserAsync(model);
        var options = _userService.StartWebAuthnLoginAssertion();

        var tokenable = new WebAuthnLoginAssertionOptionsTokenable(WebAuthnLoginAssertionOptionsScope.PrfRegistration, options);
        var token = _assertionOptionsDataProtector.Protect(tokenable);

        return new WebAuthnLoginAssertionOptionsResponseModel
        {
            Options = options,
            Token = token
        };
    }

    [HttpPost("")]
    public async Task Post([FromBody] WebAuthnLoginCredentialCreateRequestModel model)
    {
        var user = await GetUserAsync();
        await ValidateRequireSsoPolicyDisabledOrNotApplicable(user.Id);
        var tokenable = _createOptionsDataProtector.Unprotect(model.Token);

        if (!tokenable.TokenIsValid(user))
        {
            throw new BadRequestException("The token associated with your request is expired. A valid token is required to continue.");
        }

        var success = await _userService.CompleteWebAuthLoginRegistrationAsync(user, model.Name, tokenable.Options, model.DeviceResponse, model.SupportsPrf, model.EncryptedUserKey, model.EncryptedPublicKey, model.EncryptedPrivateKey);
        if (!success)
        {
            throw new BadRequestException("Unable to complete WebAuthn registration.");
        }
    }

    private async Task ValidateRequireSsoPolicyDisabledOrNotApplicable(Guid userId)
    {
        var requireSsoLogin = await _policyService.AnyPoliciesApplicableToUserAsync(userId, PolicyType.RequireSso);

        if (requireSsoLogin)
        {
            throw new BadRequestException("Passkeys cannot be created for your account. SSO login is required.");
        }
    }

    [HttpPut()]
    public async Task UpdateCredential([FromBody] WebAuthnLoginCredentialUpdateRequestModel model)
    {
        var tokenable = _assertionOptionsDataProtector.Unprotect(model.Token);
        if (!tokenable.TokenIsValid(WebAuthnLoginAssertionOptionsScope.PrfRegistration))
        {
            throw new BadRequestException("The token associated with your request is expired. A valid token is required to continue.");
        }

        var (_, credential) = await _userService.CompleteWebAuthLoginAssertionAsync(tokenable.Options, model.DeviceResponse);
        if (credential == null)
        {
            throw new BadRequestException("Unable to update WebAuthnLoginCredential.");
        }

        // assign new keys to credential
        credential.EncryptedUserKey = model.EncryptedUserKey;
        credential.EncryptedPrivateKey = model.EncryptedPrivateKey;
        credential.EncryptedPublicKey = model.EncryptedPublicKey;

        await _credentialRepository.UpdateAsync(credential);
    }

    [HttpPost("{id}/delete")]
    public async Task Delete(Guid id, [FromBody] SecretVerificationRequestModel model)
    {
        var user = await VerifyUserAsync(model);
        var credential = await _credentialRepository.GetByIdAsync(id, user.Id);
        if (credential == null)
        {
            throw new NotFoundException("Credential not found.");
        }

        await _credentialRepository.DeleteAsync(credential);
    }

    private async Task<Core.Entities.User> GetUserAsync()
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }
        return user;
    }

    private async Task<Core.Entities.User> VerifyUserAsync(SecretVerificationRequestModel model)
    {
        var user = await GetUserAsync();
        if (!await _userService.VerifySecretAsync(user, model.Secret))
        {
            await Task.Delay(Constants.FailedSecretVerificationDelay);
            throw new BadRequestException(string.Empty, "User verification failed.");
        }

        return user;
    }
}
