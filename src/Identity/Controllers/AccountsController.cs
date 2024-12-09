using System.Diagnostics;
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
using Microsoft.AspNetCore.Identity;
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
    private readonly IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> _registrationEmailVerificationTokenDataFactory;

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
        IFeatureService featureService,
        IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> registrationEmailVerificationTokenDataFactory
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
        _registrationEmailVerificationTokenDataFactory = registrationEmailVerificationTokenDataFactory;
    }

    [HttpPost("register")]
    [CaptchaProtected]
    public async Task<RegisterResponseModel> PostRegister([FromBody] RegisterRequestModel model)
    {
        var user = model.ToUser();
        var identityResult = await _registerUserCommand.RegisterUserViaOrganizationInviteToken(user, model.MasterPasswordHash,
            model.Token, model.OrganizationUserId);
        // delaysEnabled false is only for the new registration with email verification process
        return await ProcessRegistrationResult(identityResult, user, delaysEnabled: true);
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
            Source = ReferenceEventSource.Registration
        };
        await _referenceEventService.RaiseEventAsync(refEvent);

        if (token != null)
        {
            return Ok(token);
        }

        return NoContent();
    }

    [RequireFeature(FeatureFlagKeys.EmailVerification)]
    [HttpPost("register/verification-email-clicked")]
    public async Task<IActionResult> PostRegisterVerificationEmailClicked([FromBody] RegisterVerificationEmailClickedRequestModel model)
    {
        var tokenValid = RegistrationEmailVerificationTokenable.ValidateToken(_registrationEmailVerificationTokenDataFactory, model.EmailVerificationToken, model.Email);

        // Check to see if the user already exists - this is just to catch the unlikely but possible case
        // where a user finishes registration and then clicks the email verification link again.
        var user = await _userRepository.GetByEmailAsync(model.Email);
        var userExists = user != null;

        var refEvent = new ReferenceEvent
        {
            Type = ReferenceEventType.SignupEmailClicked,
            ClientId = _currentContext.ClientId,
            ClientVersion = _currentContext.ClientVersion,
            Source = ReferenceEventSource.Registration,
            EmailVerificationTokenValid = tokenValid,
            UserAlreadyExists = userExists
        };

        await _referenceEventService.RaiseEventAsync(refEvent);

        if (!tokenValid || userExists)
        {
            throw new BadRequestException("Expired link. Please restart registration or try logging in. You may already have an account");
        }

        return Ok();


    }

    [RequireFeature(FeatureFlagKeys.EmailVerification)]
    [HttpPost("register/finish")]
    public async Task<RegisterResponseModel> PostRegisterFinish([FromBody] RegisterFinishRequestModel model)
    {
        var user = model.ToUser();

        // Users will either have an emailed token or an email verification token - not both.

        IdentityResult identityResult = null;
        var delaysEnabled = !_featureService.IsEnabled(FeatureFlagKeys.EmailVerificationDisableTimingDelays);

        switch (model.GetTokenType())
        {
            case RegisterFinishTokenType.EmailVerification:
                identityResult =
                    await _registerUserCommand.RegisterUserViaEmailVerificationToken(user, model.MasterPasswordHash,
                        model.EmailVerificationToken);

                return await ProcessRegistrationResult(identityResult, user, delaysEnabled);
                break;
            case RegisterFinishTokenType.OrganizationInvite:
                identityResult = await _registerUserCommand.RegisterUserViaOrganizationInviteToken(user, model.MasterPasswordHash,
                    model.OrgInviteToken, model.OrganizationUserId);

                return await ProcessRegistrationResult(identityResult, user, delaysEnabled);
                break;
            case RegisterFinishTokenType.OrgSponsoredFreeFamilyPlan:
                identityResult = await _registerUserCommand.RegisterUserViaOrganizationSponsoredFreeFamilyPlanInviteToken(user, model.MasterPasswordHash, model.OrgSponsoredFreeFamilyPlanToken);

                return await ProcessRegistrationResult(identityResult, user, delaysEnabled);
                break;
            case RegisterFinishTokenType.EmergencyAccessInvite:
                Debug.Assert(model.AcceptEmergencyAccessId.HasValue);
                identityResult = await _registerUserCommand.RegisterUserViaAcceptEmergencyAccessInviteToken(user, model.MasterPasswordHash,
                    model.AcceptEmergencyAccessInviteToken, model.AcceptEmergencyAccessId.Value);

                return await ProcessRegistrationResult(identityResult, user, delaysEnabled);
                break;
            case RegisterFinishTokenType.ProviderInvite:
                Debug.Assert(model.ProviderUserId.HasValue);
                identityResult = await _registerUserCommand.RegisterUserViaProviderInviteToken(user, model.MasterPasswordHash,
                    model.ProviderInviteToken, model.ProviderUserId.Value);

                return await ProcessRegistrationResult(identityResult, user, delaysEnabled);
                break;

            default:
                throw new BadRequestException("Invalid registration finish request");
        }
    }

    private async Task<RegisterResponseModel> ProcessRegistrationResult(IdentityResult result, User user, bool delaysEnabled)
    {
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
