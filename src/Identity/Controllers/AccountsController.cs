using Bit.Core;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Api.Response.Accounts;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Services;
using Bit.Core.Auth.UserFeatures.WebAuthnLogin;
using Bit.Core.Auth.Utilities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Identity.Controllers;

[Route("accounts")]
[ExceptionHandlerFilter]
public class AccountsController : Controller
{
    private readonly ILogger<AccountsController> _logger;
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly ICaptchaValidationService _captchaValidationService;
    private readonly IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable> _assertionOptionsDataProtector;
    private readonly IGetWebAuthnLoginCredentialAssertionOptionsCommand _getWebAuthnLoginCredentialAssertionOptionsCommand;
    private readonly GlobalSettings _globalSettings;

    public AccountsController(
        ILogger<AccountsController> logger,
        IUserRepository userRepository,
        IUserService userService,
        ICaptchaValidationService captchaValidationService,
        IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable> assertionOptionsDataProtector,
        IGetWebAuthnLoginCredentialAssertionOptionsCommand getWebAuthnLoginCredentialAssertionOptionsCommand,
        GlobalSettings globalSettings
        )
    {
        _logger = logger;
        _userRepository = userRepository;
        _userService = userService;
        _captchaValidationService = captchaValidationService;
        _assertionOptionsDataProtector = assertionOptionsDataProtector;
        _getWebAuthnLoginCredentialAssertionOptionsCommand = getWebAuthnLoginCredentialAssertionOptionsCommand;
        _globalSettings = globalSettings;
    }

    // Moved from API, If you modify this endpoint, please update API as well. Self hosted installs still use the API endpoints.
    [HttpPost("register")]
    [CaptchaProtected]
    public async Task<RegisterResponseModel> PostRegister([FromBody] RegisterRequestModel model)
    {
        var user = model.ToUser();
        var result = await _userService.RegisterUserAsync(user, model.MasterPasswordHash,
            model.Token, model.OrganizationUserId);
        if (result.Succeeded)
        {
            var captchaBypassToken = _captchaValidationService.GenerateCaptchaBypassToken(user);
            return new RegisterResponseModel(captchaBypassToken);
        }

        foreach (var error in result.Errors.Where(e => e.Code != "DuplicateUserName"))
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        await Task.Delay(2000);
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

    [RequireFeature(FeatureFlagKeys.EmailVerification)]
    [HttpPost("register/send-email-verification")]
    public async Task<IActionResult> PostRegisterSendEmailVerification([FromBody] RegisterSendEmailVerificationRequestModel model)
    {
        //TODO: ask about reference data (tracking information that we collect today) and how it will be handled as part of this endpoint
        if (!ModelState.IsValid)
        {
            // return BadRequest(ModelState);
            throw new BadRequestException(ModelState);
        }

        // Check to see if the user already exists
        var user = await _userRepository.GetByEmailAsync(model.Email);

        if (_globalSettings.EnableEmailVerification)
        {
            // TODO: create command to execute this
            // If the user doesn't exist, create a new EmailVerificationTokenable and send the user
            // an email with a link to verify their email address
            // return a 200 regardless of whether the email was sent or not
            return Ok();
        }
        else
        {
            // TODO: create command to execute this
            // if email exists, return the same error as existing endpoint to user
            // if email doesn't exist, return with a EmailVerificationTokenable in the response body.
        }

        // Add a standard delay to prevent timing attacks but only in error scenarios
        await Task.Delay(2000);

        return Ok();
    }
}
