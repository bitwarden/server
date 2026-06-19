// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Api.Models.Request;
using Bit.Api.Models.Response;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Services;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Auth.Controllers;

[Route("two-factor")]
[Authorize(Policies.Web)]
public class TwoFactorController : Controller
{
    private readonly IUserService _userService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationService _organizationService;
    private readonly UserManager<User> _userManager;
    private readonly ICurrentContext _currentContext;
    private readonly IAuthRequestRepository _authRequestRepository;
    private readonly IDuoUniversalTokenService _duoUniversalTokenService;
    private readonly IDataProtectorTokenFactory<TwoFactorAuthenticatorUserVerificationTokenable> _twoFactorAuthenticatorDataProtector;
    private readonly IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable> _twoFactorUserVerificationDataProtector;
    private readonly ITwoFactorUserVerificationTokenableFactory _twoFactorUserVerificationTokenableFactory;
    private readonly IDataProtectorTokenFactory<SsoEmail2faSessionTokenable> _ssoEmailTwoFactorSessionDataProtector;
    private readonly ITwoFactorEmailService _twoFactorEmailService;
    private readonly IStartTwoFactorWebAuthnRegistrationCommand _startTwoFactorWebAuthnRegistrationCommand;
    private readonly ICompleteTwoFactorWebAuthnRegistrationCommand _completeTwoFactorWebAuthnRegistrationCommand;
    private readonly IDeleteTwoFactorWebAuthnCredentialCommand _deleteTwoFactorWebAuthnCredentialCommand;

    public TwoFactorController(
        IUserService userService,
        IOrganizationRepository organizationRepository,
        IOrganizationService organizationService,
        UserManager<User> userManager,
        ICurrentContext currentContext,
        IAuthRequestRepository authRequestRepository,
        IDuoUniversalTokenService duoUniversalConfigService,
        IDataProtectorTokenFactory<TwoFactorAuthenticatorUserVerificationTokenable> twoFactorAuthenticatorDataProtector,
        IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable> twoFactorUserVerificationDataProtector,
        ITwoFactorUserVerificationTokenableFactory twoFactorUserVerificationTokenableFactory,
        IDataProtectorTokenFactory<SsoEmail2faSessionTokenable> ssoEmailTwoFactorSessionDataProtector,
        ITwoFactorEmailService twoFactorEmailService,
        IStartTwoFactorWebAuthnRegistrationCommand startTwoFactorWebAuthnRegistrationCommand,
        ICompleteTwoFactorWebAuthnRegistrationCommand completeTwoFactorWebAuthnRegistrationCommand,
        IDeleteTwoFactorWebAuthnCredentialCommand deleteTwoFactorWebAuthnCredentialCommand)
    {
        _userService = userService;
        _organizationRepository = organizationRepository;
        _organizationService = organizationService;
        _userManager = userManager;
        _currentContext = currentContext;
        _authRequestRepository = authRequestRepository;
        _duoUniversalTokenService = duoUniversalConfigService;
        _twoFactorAuthenticatorDataProtector = twoFactorAuthenticatorDataProtector;
        _twoFactorUserVerificationDataProtector = twoFactorUserVerificationDataProtector;
        _twoFactorUserVerificationTokenableFactory = twoFactorUserVerificationTokenableFactory;
        _ssoEmailTwoFactorSessionDataProtector = ssoEmailTwoFactorSessionDataProtector;
        _twoFactorEmailService = twoFactorEmailService;
        _startTwoFactorWebAuthnRegistrationCommand = startTwoFactorWebAuthnRegistrationCommand;
        _completeTwoFactorWebAuthnRegistrationCommand = completeTwoFactorWebAuthnRegistrationCommand;
        _deleteTwoFactorWebAuthnCredentialCommand = deleteTwoFactorWebAuthnCredentialCommand;
    }

    [HttpGet("")]
    public async Task<ListResponseModel<TwoFactorProviderResponseModel>> Get()
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var providers = user.GetTwoFactorProviders()?.Select(
            p => new TwoFactorProviderResponseModel(p.Key, p.Value));
        return new ListResponseModel<TwoFactorProviderResponseModel>(providers);
    }

    [HttpGet("~/organizations/{id}/two-factor")]
    public async Task<ListResponseModel<TwoFactorProviderResponseModel>> GetOrganization(string id)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.OrganizationAdmin(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        var providers = organization.GetTwoFactorProviders()?.Select(
            p => new TwoFactorProviderResponseModel(p.Key, p.Value));
        return new ListResponseModel<TwoFactorProviderResponseModel>(providers);
    }

    [HttpPost("get-authenticator")]
    public async Task<TwoFactorAuthenticatorResponseModel> GetAuthenticator(
        [FromBody] SecretVerificationRequestModel model)
    {
        var user = await ValidateUserBySecretAsync(model);
        var response = new TwoFactorAuthenticatorResponseModel(user);
        var tokenable = new TwoFactorAuthenticatorUserVerificationTokenable(user, response.Key);
        response.UserVerificationToken = _twoFactorAuthenticatorDataProtector.Protect(tokenable);
        return response;
    }

    [HttpPut("authenticator")]
    public async Task<TwoFactorAuthenticatorResponseModel> PutAuthenticator(
        [FromBody] UpdateTwoFactorAuthenticatorRequestModel model)
    {
        var user = model.ToUser(await _userService.GetUserByPrincipalAsync(User));

        var tokenIsValid =
            _twoFactorAuthenticatorDataProtector.TryUnprotect(model.UserVerificationToken, out var decryptedToken)
            && decryptedToken.Valid
            && decryptedToken.TokenIsValid(user, model.Key);

        if (!tokenIsValid)
        {
            throw new BadRequestException("UserVerificationToken", "User verification failed.");
        }

        if (!await _userManager.VerifyTwoFactorTokenAsync(user,
                CoreHelpers.CustomProviderName(TwoFactorProviderType.Authenticator), model.Token))
        {
            throw new BadRequestException("Token", "Invalid token.");
        }

        await _userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Authenticator);
        var response = new TwoFactorAuthenticatorResponseModel(user);
        return response;
    }

    [HttpPost("authenticator")]
    [Obsolete("This endpoint is deprecated. Use PUT /authenticator instead.")]
    public async Task<TwoFactorAuthenticatorResponseModel> PostAuthenticator(
        [FromBody] UpdateTwoFactorAuthenticatorRequestModel model)
    {
        return await PutAuthenticator(model);
    }

    [HttpDelete("authenticator")]
    public async Task<TwoFactorProviderResponseModel> DisableAuthenticator(
        [FromBody] TwoFactorAuthenticatorDisableRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);

        var tokenIsValid =
            _twoFactorAuthenticatorDataProtector.TryUnprotect(model.UserVerificationToken, out var decryptedToken)
            && decryptedToken.Valid
            && decryptedToken.TokenIsValid(user, model.Key);

        if (!tokenIsValid)
        {
            throw new BadRequestException("UserVerificationToken", "User verification failed.");
        }

        await _userService.DisableTwoFactorProviderAsync(user, TwoFactorProviderType.Authenticator);
        return new TwoFactorProviderResponseModel(TwoFactorProviderType.Authenticator, user);
    }

    [HttpPost("get-yubikey")]
    public async Task<TwoFactorYubiKeyResponseModel> GetYubiKey([FromBody] SecretVerificationRequestModel model)
    {
        var user = await ValidateUserBySecretAsync(model);
        return new TwoFactorYubiKeyResponseModel(user)
        {
            UserVerificationToken = MintProtectedUserVerificationToken(user, TwoFactorProviderType.YubiKey),
        };
    }

    [HttpDelete("yubikey")]
    public async Task<TwoFactorProviderResponseModel> DisableYubiKey([FromBody] TwoFactorYubiKeyDisableRequestModel model)
    {
        var user = await ValidateUserVerificationTokenAsync(model.UserVerificationToken, TwoFactorProviderType.YubiKey);
        await _userService.DisableTwoFactorProviderAsync(user, TwoFactorProviderType.YubiKey);
        return new TwoFactorProviderResponseModel(TwoFactorProviderType.YubiKey, user);
    }

    [HttpPut("yubikey")]
    public async Task<TwoFactorYubiKeyResponseModel> PutYubiKey([FromBody] UpdateTwoFactorYubicoOtpRequestModel model)
    {
        var user = await ValidateUserVerificationTokenAsync(model.UserVerificationToken, TwoFactorProviderType.YubiKey);
        await ValidateUserHasPremiumAsync(user);
        model.ToUser(user);

        await ValidateYubiKeyAsync(user, nameof(model.Key1), model.Key1);
        await ValidateYubiKeyAsync(user, nameof(model.Key2), model.Key2);
        await ValidateYubiKeyAsync(user, nameof(model.Key3), model.Key3);
        await ValidateYubiKeyAsync(user, nameof(model.Key4), model.Key4);
        await ValidateYubiKeyAsync(user, nameof(model.Key5), model.Key5);

        await _userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.YubiKey);
        var response = new TwoFactorYubiKeyResponseModel(user);
        return response;
    }

    [HttpPost("yubikey")]
    [Obsolete("This endpoint is deprecated. Use PUT /yubikey instead.")]
    public async Task<TwoFactorYubiKeyResponseModel> PostYubiKey([FromBody] UpdateTwoFactorYubicoOtpRequestModel model)
    {
        return await PutYubiKey(model);
    }

    [HttpPost("get-duo")]
    public async Task<TwoFactorDuoResponseModel> GetDuo([FromBody] SecretVerificationRequestModel model)
    {
        var user = await ValidateUserBySecretAsync(model);
        return new TwoFactorDuoResponseModel(user)
        {
            UserVerificationToken = MintProtectedUserVerificationToken(user, TwoFactorProviderType.Duo),
        };
    }

    [HttpDelete("duo")]
    public async Task<TwoFactorProviderResponseModel> DisableDuo([FromBody] TwoFactorDuoDisableRequestModel model)
    {
        var user = await ValidateUserVerificationTokenAsync(model.UserVerificationToken, TwoFactorProviderType.Duo);
        await _userService.DisableTwoFactorProviderAsync(user, TwoFactorProviderType.Duo);
        return new TwoFactorProviderResponseModel(TwoFactorProviderType.Duo, user);
    }

    [HttpPut("duo")]
    public async Task<TwoFactorDuoResponseModel> PutDuo([FromBody] UpdateTwoFactorDuoRequestModel model)
    {
        var user = await ValidateUserVerificationTokenAsync(model.UserVerificationToken, TwoFactorProviderType.Duo);
        await ValidateUserHasPremiumAsync(user);
        if (!await _duoUniversalTokenService.ValidateDuoConfiguration(model.ClientSecret, model.ClientId, model.Host))
        {
            throw new BadRequestException(
                "Duo configuration settings are not valid. Please re-check the Duo Admin panel.");
        }

        model.ToUser(user);
        await _userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Duo);
        var response = new TwoFactorDuoResponseModel(user);
        return response;
    }

    [HttpPost("duo")]
    [Obsolete("This endpoint is deprecated. Use PUT /duo instead.")]
    public async Task<TwoFactorDuoResponseModel> PostDuo([FromBody] UpdateTwoFactorDuoRequestModel model)
    {
        return await PutDuo(model);
    }

    [HttpPost("~/organizations/{id}/two-factor/get-duo")]
    public async Task<TwoFactorDuoResponseModel> GetOrganizationDuo(string id,
        [FromBody] SecretVerificationRequestModel model)
    {
        var user = await ValidateUserBySecretAsync(model);

        var orgIdGuid = new Guid(id);
        if (!await _currentContext.ManagePolicies(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var organization = await _organizationRepository.GetByIdAsync(orgIdGuid) ?? throw new NotFoundException();
        return new TwoFactorDuoResponseModel(organization)
        {
            UserVerificationToken = MintProtectedUserVerificationToken(user, TwoFactorProviderType.OrganizationDuo),
        };
    }

    [HttpDelete("~/organizations/{id}/two-factor/duo")]
    public async Task<TwoFactorProviderResponseModel> DisableOrganizationDuo(string id,
        [FromBody] TwoFactorOrganizationDuoDisableRequestModel model)
    {
        await ValidateUserVerificationTokenAsync(model.UserVerificationToken, TwoFactorProviderType.OrganizationDuo);

        var orgIdGuid = new Guid(id);
        if (!await _currentContext.ManagePolicies(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var organization = await _organizationRepository.GetByIdAsync(orgIdGuid) ?? throw new NotFoundException();
        await _organizationService.DisableTwoFactorProviderAsync(organization, TwoFactorProviderType.OrganizationDuo);
        return new TwoFactorProviderResponseModel(TwoFactorProviderType.OrganizationDuo, organization);
    }

    [HttpPut("~/organizations/{id}/two-factor/duo")]
    public async Task<TwoFactorDuoResponseModel> PutOrganizationDuo(string id,
        [FromBody] UpdateTwoFactorDuoRequestModel model)
    {
        await ValidateUserVerificationTokenAsync(model.UserVerificationToken, TwoFactorProviderType.OrganizationDuo);

        var orgIdGuid = new Guid(id);
        if (!await _currentContext.ManagePolicies(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var organization = await _organizationRepository.GetByIdAsync(orgIdGuid) ?? throw new NotFoundException();
        if (!await _duoUniversalTokenService.ValidateDuoConfiguration(model.ClientSecret, model.ClientId, model.Host))
        {
            throw new BadRequestException(
                "Duo configuration settings are not valid. Please re-check the Duo Admin panel.");
        }

        model.ToOrganization(organization);
        await _organizationService.UpdateTwoFactorProviderAsync(organization,
            TwoFactorProviderType.OrganizationDuo);
        var response = new TwoFactorDuoResponseModel(organization);
        return response;
    }

    [HttpPost("~/organizations/{id}/two-factor/duo")]
    [Obsolete("This endpoint is deprecated. Use PUT /organizations/{id}/two-factor/duo instead.")]
    public async Task<TwoFactorDuoResponseModel> PostOrganizationDuo(string id,
        [FromBody] UpdateTwoFactorDuoRequestModel model)
    {
        return await PutOrganizationDuo(id, model);
    }

    [HttpPost("get-webauthn")]
    public async Task<TwoFactorWebAuthnResponseModel> GetWebAuthn([FromBody] SecretVerificationRequestModel model)
    {
        var user = await ValidateUserBySecretAsync(model);
        return new TwoFactorWebAuthnResponseModel(user)
        {
            UserVerificationToken = MintProtectedUserVerificationToken(user, TwoFactorProviderType.WebAuthn),
        };
    }

    [HttpPost("get-webauthn-challenge")]
    [ApiExplorerSettings(IgnoreApi = true)] // Disable Swagger due to CredentialCreateOptions not converting properly
    public async Task<TwoFactorWebAuthnChallengeResponseModel> GetWebAuthnChallenge([FromBody] SecretVerificationRequestModel model)
    {
        var user = await ValidateUserBySecretAsync(model);
        var options = await _startTwoFactorWebAuthnRegistrationCommand.StartTwoFactorWebAuthnRegistrationAsync(user);
        return new TwoFactorWebAuthnChallengeResponseModel
        {
            Options = options,
            UserVerificationToken = MintProtectedUserVerificationToken(user, TwoFactorProviderType.WebAuthn),
        };
    }

    [HttpPut("webauthn")]
    public async Task<TwoFactorWebAuthnResponseModel> PutWebAuthn([FromBody] TwoFactorWebAuthnRequestModel model)
    {
        var user = await ValidateUserVerificationTokenAsync(model.UserVerificationToken, TwoFactorProviderType.WebAuthn);

        var success = await _completeTwoFactorWebAuthnRegistrationCommand.CompleteTwoFactorWebAuthnRegistrationAsync(
            user, model.Id.Value, model.Name, model.DeviceResponse);
        if (!success)
        {
            throw new BadRequestException("Unable to complete WebAuthn registration.");
        }

        var response = new TwoFactorWebAuthnResponseModel(user);
        return response;
    }

    [HttpPost("webauthn")]
    [Obsolete("This endpoint is deprecated. Use PUT /webauthn instead.")]
    public async Task<TwoFactorWebAuthnResponseModel> PostWebAuthn([FromBody] TwoFactorWebAuthnRequestModel model)
    {
        return await PutWebAuthn(model);
    }

    [HttpDelete("webauthn")]
    public async Task<TwoFactorWebAuthnResponseModel> DeleteWebAuthn(
        [FromBody] TwoFactorWebAuthnDeleteRequestModel model)
    {
        var user = await ValidateUserVerificationTokenAsync(model.UserVerificationToken, TwoFactorProviderType.WebAuthn);

        if (!model.Id.HasValue)
        {
            throw new BadRequestException("Unable to delete WebAuthn credential.");
        }

        var success = await _deleteTwoFactorWebAuthnCredentialCommand.DeleteTwoFactorWebAuthnCredentialAsync(user, model.Id.Value);
        if (!success)
        {
            throw new BadRequestException("Unable to delete WebAuthn credential.");
        }

        var response = new TwoFactorWebAuthnResponseModel(user);
        return response;
    }

    [HttpDelete("webauthn/all")]
    public async Task<TwoFactorProviderResponseModel> DisableWebAuthnAll(
        [FromBody] TwoFactorWebAuthnDisableAllRequestModel model)
    {
        var user = await ValidateUserVerificationTokenAsync(model.UserVerificationToken, TwoFactorProviderType.WebAuthn);
        await _userService.DisableTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn);
        return new TwoFactorProviderResponseModel(TwoFactorProviderType.WebAuthn, user);
    }

    [HttpPost("get-email")]
    public async Task<TwoFactorEmailResponseModel> GetEmail([FromBody] SecretVerificationRequestModel model)
    {
        var user = await ValidateUserBySecretAsync(model);
        return new TwoFactorEmailResponseModel(user)
        {
            UserVerificationToken = MintProtectedUserVerificationToken(user, TwoFactorProviderType.Email),
        };
    }

    [HttpDelete("email")]
    public async Task<TwoFactorProviderResponseModel> DisableEmail([FromBody] TwoFactorEmailDisableRequestModel model)
    {
        var user = await ValidateUserVerificationTokenAsync(model.UserVerificationToken, TwoFactorProviderType.Email);
        await _userService.DisableTwoFactorProviderAsync(user, TwoFactorProviderType.Email);
        return new TwoFactorProviderResponseModel(TwoFactorProviderType.Email, user);
    }

    /// <summary>
    /// This endpoint is only used to set-up email two factor authentication. The client must first
    /// call <c>get-email</c> to obtain a user-verification token, then replay that token here.
    /// </summary>
    [HttpPost("send-email")]
    public async Task SendEmail([FromBody] TwoFactorEmailRequestModel model)
    {
        var user = await ValidateUserVerificationTokenAsync(model.UserVerificationToken, TwoFactorProviderType.Email);
        // Add email to the user's 2FA providers, with the email address they've provided.
        model.ToUser(user);
        await _twoFactorEmailService.SendTwoFactorSetupEmailAsync(user);
    }

    [AllowAnonymous]
    [HttpPost("send-email-login")]
    public async Task SendEmailLoginAsync([FromBody] TwoFactorEmailRequestModel requestModel)
    {
        var user = await _userManager.FindByEmailAsync(requestModel.Email.ToLowerInvariant());

        if (user != null)
        {
            // Check if 2FA email is from a device approval ("Log in with device") scenario.
            if (!string.IsNullOrEmpty(requestModel.AuthRequestAccessCode))
            {
                var authRequest = await _authRequestRepository.GetByIdAsync(new Guid(requestModel.AuthRequestId));
                if (authRequest != null &&
                    authRequest.IsValidForAuthentication(user.Id, requestModel.AuthRequestAccessCode))
                {
                    await _twoFactorEmailService.SendTwoFactorEmailAsync(user);
                    return;
                }
            }
            else if (!string.IsNullOrEmpty(requestModel.SsoEmail2FaSessionToken))
            {
                if (ValidateSsoEmail2FaToken(requestModel.SsoEmail2FaSessionToken, user))
                {
                    await _twoFactorEmailService.SendTwoFactorEmailAsync(user);
                    return;
                }

                await ThrowDelayedBadRequestExceptionAsync(
                    "Cannot send two-factor email: a valid, non-expired SSO Email 2FA Session token is required to send 2FA emails.");
            }
            else if (await _userService.VerifySecretAsync(user, requestModel.Secret))
            {
                await _twoFactorEmailService.SendTwoFactorEmailAsync(user);
                return;
            }
        }

        await ThrowDelayedBadRequestExceptionAsync("Cannot send two-factor email.");
    }

    [HttpPut("email")]
    public async Task<TwoFactorEmailResponseModel> PutEmail([FromBody] UpdateTwoFactorEmailRequestModel model)
    {
        var user = await ValidateUserVerificationTokenAsync(model.UserVerificationToken, TwoFactorProviderType.Email);
        model.ToUser(user);

        if (!await _userManager.VerifyTwoFactorTokenAsync(user,
                CoreHelpers.CustomProviderName(TwoFactorProviderType.Email), model.Token))
        {
            throw new BadRequestException("Token", "Invalid token.");
        }

        await _userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Email);
        var response = new TwoFactorEmailResponseModel(user);
        return response;
    }

    [HttpPost("email")]
    [Obsolete("This endpoint is deprecated. Use PUT /email instead.")]
    public async Task<TwoFactorEmailResponseModel> PostEmail([FromBody] UpdateTwoFactorEmailRequestModel model)
    {
        return await PutEmail(model);
    }

    [HttpPost("get-recover")]
    public async Task<TwoFactorRecoverResponseModel> GetRecover([FromBody] SecretVerificationRequestModel model)
    {
        var user = await ValidateUserBySecretAsync(model);
        var response = new TwoFactorRecoverResponseModel(user);
        return response;
    }

    [Obsolete("Leaving this for backwards compatibility on clients")]
    [HttpGet("get-device-verification-settings")]
    public Task<DeviceVerificationResponseModel> GetDeviceVerificationSettings()
    {
        return Task.FromResult(new DeviceVerificationResponseModel(false, false));
    }

    [Obsolete("Leaving this for backwards compatibility on clients")]
    [HttpPut("device-verification-settings")]
    public Task<DeviceVerificationResponseModel> PutDeviceVerificationSettings(
        [FromBody] DeviceVerificationRequestModel model)
    {
        return Task.FromResult(new DeviceVerificationResponseModel(false, false));
    }

    /// <summary>Verifies the principal user's secret (master password or OTP) and returns the user.</summary>
    /// <exception cref="UnauthorizedAccessException">No authenticated user.</exception>
    /// <exception cref="BadRequestException">Secret does not verify.</exception>
    private async Task<User> ValidateUserBySecretAsync(SecretVerificationRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        if (!await _userService.VerifySecretAsync(user, model.Secret))
        {
            throw new BadRequestException(string.Empty, "User verification failed.");
        }
        return user;
    }

    /// <summary>Verifies a user-verification token is bound to the principal user and the given provider, and returns the user.</summary>
    /// <exception cref="UnauthorizedAccessException">No authenticated user.</exception>
    /// <exception cref="BadRequestException">Token cannot be unprotected, has expired, or is not bound to this user and provider.</exception>
    private async Task<User> ValidateUserVerificationTokenAsync(string userVerificationToken, TwoFactorProviderType providerType)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        if (!TwoFactorUserVerificationTokenable.Validate(
                _twoFactorUserVerificationDataProtector, userVerificationToken, user, providerType))
        {
            throw new BadRequestException("UserVerificationToken", "User verification failed.");
        }
        return user;
    }

    /// <summary>Verifies the user has premium access.</summary>
    /// <exception cref="BadRequestException">User cannot access premium.</exception>
    private async Task ValidateUserHasPremiumAsync(User user)
    {
        if (!await _userService.CanAccessPremium(user))
        {
            throw new BadRequestException("Premium status is required.");
        }
    }

    /// <summary>Mints a protected user-verification token bound to <paramref name="user"/> and <paramref name="providerType"/>.</summary>
    private string MintProtectedUserVerificationToken(User user, TwoFactorProviderType providerType)
    {
        var token = _twoFactorUserVerificationTokenableFactory.CreateToken(user, providerType);
        return _twoFactorUserVerificationDataProtector.Protect(token);
    }

    private async Task ValidateYubiKeyAsync(User user, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length == 12)
        {
            return;
        }

        if (!await _userManager.VerifyTwoFactorTokenAsync(user,
                CoreHelpers.CustomProviderName(TwoFactorProviderType.YubiKey), value))
        {
            await Task.Delay(2000);
            throw new BadRequestException(name, $"{name} is invalid.");
        }

        await Task.Delay(500);
    }

    private bool ValidateSsoEmail2FaToken(string ssoEmail2FaSessionToken, User user)
    {
        return _ssoEmailTwoFactorSessionDataProtector.TryUnprotect(ssoEmail2FaSessionToken, out var decryptedToken) &&
               decryptedToken.Valid && decryptedToken.TokenIsValid(user);
    }

    private async Task ThrowDelayedBadRequestExceptionAsync(string message, int delayTime = 2000)
    {
        await Task.Delay(delayTime);
        throw new BadRequestException(message);
    }
}
