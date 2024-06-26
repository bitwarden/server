using Bit.Core;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Api.Response.Accounts;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Services;
using Bit.Core.Auth.UserFeatures.Registration;
using Bit.Core.Auth.UserFeatures.WebAuthnLogin;
using Bit.Core.Auth.Utilities;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Bit.Identity.Models.Request.Accounts;
using Bit.Identity.Models.Response.Accounts;
using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Identity.Controllers;

[Route("accounts")]
[ExceptionHandlerFilter]
public class AccountsController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly ILogger<AccountsController> _logger;
    private readonly IUserRepository _userRepository;
    private readonly IRegisterUserCommand _registerUserCommand;
    private readonly ICaptchaValidationService _captchaValidationService;
    private readonly IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable> _assertionOptionsDataProtector;
    private readonly IGetWebAuthnLoginCredentialAssertionOptionsCommand _getWebAuthnLoginCredentialAssertionOptionsCommand;
    private readonly ISendVerificationEmailForRegistrationCommand _sendVerificationEmailForRegistrationCommand;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IFeatureService _featureService;

    public AccountsController(
        ICurrentContext currentContext,
        ILogger<AccountsController> logger,
        IUserRepository userRepository,
        IRegisterUserCommand registerUserCommand,
        ICaptchaValidationService captchaValidationService,
        IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable> assertionOptionsDataProtector,
        IGetWebAuthnLoginCredentialAssertionOptionsCommand getWebAuthnLoginCredentialAssertionOptionsCommand,
        ISendVerificationEmailForRegistrationCommand sendVerificationEmailForRegistrationCommand,
        IReferenceEventService referenceEventService,
        IFeatureService featureService
        )
    {
        _currentContext = currentContext;
        _logger = logger;
        _userRepository = userRepository;
        _registerUserCommand = registerUserCommand;
        _captchaValidationService = captchaValidationService;
        _assertionOptionsDataProtector = assertionOptionsDataProtector;
        _getWebAuthnLoginCredentialAssertionOptionsCommand = getWebAuthnLoginCredentialAssertionOptionsCommand;
        _sendVerificationEmailForRegistrationCommand = sendVerificationEmailForRegistrationCommand;
        _referenceEventService = referenceEventService;
        _featureService = featureService;
    }

    [HttpPost("register")]
    [CaptchaProtected]
    public async Task<RegisterResponseModel> PostRegister([FromBody] RegisterRequestModel model)
    {
        var user = model.ToUser();
        var delaysEnabled = !_featureService.IsEnabled(FeatureFlagKeys.EmailVerificationDisableTimingDelays);
        var registerResponseModel = await RegisterUserWithOptionalOrgInvite(user, model.MasterPasswordHash, model.Token, model.OrganizationUserId, delaysEnabled);
        return registerResponseModel;
    }

    [RequireFeature(FeatureFlagKeys.EmailVerification)]
    [HttpPost("register/send-verification-email")]
    public async Task<IActionResult> PostRegisterSendVerificationEmail([FromBody] RegisterSendVerificationEmailRequestModel model)
    {
        var token = await _sendVerificationEmailForRegistrationCommand.Run(model.Email, model.Name,
            model.ReceiveMarketingEmails);

        var refEvent = new ReferenceEvent
        {
            Type = ReferenceEventType.SignupEmailSubmit,
            ClientId = _currentContext.ClientId,
            ClientVersion = _currentContext.ClientVersion,
            Source = ReferenceEventSource.RegistrationStart
        };
        await _referenceEventService.RaiseEventAsync(refEvent);

        if (token != null)
        {
            return Ok(token);
        }

        return NoContent();
    }

    [RequireFeature(FeatureFlagKeys.EmailVerification)]
    [HttpPost("register/finish")]
    public async Task<RegisterResponseModel> PostRegisterFinish([FromBody] RegisterFinishRequestModel model)
    {
        var user = model.ToUser();

        // Users will either have an org invite token or an email verification token - not both.

        var delaysEnabled = !_featureService.IsEnabled(FeatureFlagKeys.EmailVerificationDisableTimingDelays);

        if (!string.IsNullOrEmpty(model.OrgInviteToken) && model.OrganizationUserId.HasValue)
        {
            var result = await RegisterUserWithOptionalOrgInvite(user, model.MasterPasswordHash, model.OrgInviteToken, model.OrganizationUserId, delaysEnabled);
            return result;
        }

        var identityResult = await _registerUserCommand.RegisterUserViaEmailVerificationToken(user, model.MasterPasswordHash, model.EmailVerificationToken);

        if (identityResult.Succeeded)
        {

            var captchaBypassToken = _captchaValidationService.GenerateCaptchaBypassToken(user);

            return new RegisterResponseModel(captchaBypassToken);

        }

        if (delaysEnabled)
        {
            await Task.Delay(Random.Shared.Next(100, 130));
        }
        throw new BadRequestException("An unexpected error occurred. Unable to register user. Please try again.");
    }

    private async Task<RegisterResponseModel> RegisterUserWithOptionalOrgInvite(User user, string masterPasswordHash, string orgInviteToken, Guid? orgUserId, bool delaysEnabled = true)
    {
        var result = await _registerUserCommand.RegisterUserWithOptionalOrgInvite(user, masterPasswordHash,
            orgInviteToken, orgUserId);
        if (result.Succeeded)
        {
            var captchaBypassToken = _captchaValidationService.GenerateCaptchaBypassToken(user);
            return new RegisterResponseModel(captchaBypassToken);
        }

        foreach (var error in result.Errors.Where(e => e.Code != "DuplicateUserName"))
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        if (delaysEnabled)
        {
            await Task.Delay(Random.Shared.Next(100, 130));
        }
        throw new BadRequestException(ModelState);
    }

    // Moved from API, If you modify this endpoint, please update API as well. Self hosted installs still use the API endpoints.
    [HttpPost("prelogin")]
    public async Task<PreloginResponseModel> PostPrelogin([FromBody] PreloginRequestModel model)
    {
        var kdfInformation = await _userRepository.GetKdfInformationByEmailAsync(model.Email);
        if (kdfInformation == null)
        {
            kdfInformation = new UserKdfInformation
            {
                Kdf = KdfType.PBKDF2_SHA256,
                KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            };
        }
        return new PreloginResponseModel(kdfInformation);
    }

    [HttpGet("webauthn/assertion-options")]
    public WebAuthnLoginAssertionOptionsResponseModel GetWebAuthnLoginAssertionOptions()
    {
        var options = _getWebAuthnLoginCredentialAssertionOptionsCommand.GetWebAuthnLoginCredentialAssertionOptions();

        var tokenable = new WebAuthnLoginAssertionOptionsTokenable(WebAuthnLoginAssertionOptionsScope.Authentication, options);
        var token = _assertionOptionsDataProtector.Protect(tokenable);

        return new WebAuthnLoginAssertionOptionsResponseModel
        {
            Options = options,
            Token = token
        };
    }
}
