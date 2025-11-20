// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Diagnostics;
using System.Text;
using Bit.Core;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Api.Response.Accounts;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.UserFeatures.Registration;
using Bit.Core.Auth.UserFeatures.WebAuthnLogin;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
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
    private readonly IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable> _assertionOptionsDataProtector;
    private readonly IGetWebAuthnLoginCredentialAssertionOptionsCommand _getWebAuthnLoginCredentialAssertionOptionsCommand;
    private readonly ISendVerificationEmailForRegistrationCommand _sendVerificationEmailForRegistrationCommand;
    private readonly IFeatureService _featureService;
    private readonly IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> _registrationEmailVerificationTokenDataFactory;

    private readonly byte[] _defaultKdfHmacKey = null;
    private static readonly List<UserKdfInformation> _defaultKdfResults =
    [
        // The first result (index 0) should always return the "normal" default.
        new()
        {
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
        },
        // We want more weight for this default, so add it again
        new()
        {
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
        },
        // Add some other possible defaults...
        new()
        {
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 100_000,
        },
        new()
        {
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 5_000,
        },
        new()
        {
            Kdf = KdfType.Argon2id,
            KdfIterations = AuthConstants.ARGON2_ITERATIONS.Default,
            KdfMemory = AuthConstants.ARGON2_MEMORY.Default,
            KdfParallelism = AuthConstants.ARGON2_PARALLELISM.Default,
        }
    ];

    public AccountsController(
        ICurrentContext currentContext,
        ILogger<AccountsController> logger,
        IUserRepository userRepository,
        IRegisterUserCommand registerUserCommand,
        IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable> assertionOptionsDataProtector,
        IGetWebAuthnLoginCredentialAssertionOptionsCommand getWebAuthnLoginCredentialAssertionOptionsCommand,
        ISendVerificationEmailForRegistrationCommand sendVerificationEmailForRegistrationCommand,
        IFeatureService featureService,
        IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> registrationEmailVerificationTokenDataFactory,
        GlobalSettings globalSettings
        )
    {
        _currentContext = currentContext;
        _logger = logger;
        _userRepository = userRepository;
        _registerUserCommand = registerUserCommand;
        _assertionOptionsDataProtector = assertionOptionsDataProtector;
        _getWebAuthnLoginCredentialAssertionOptionsCommand = getWebAuthnLoginCredentialAssertionOptionsCommand;
        _sendVerificationEmailForRegistrationCommand = sendVerificationEmailForRegistrationCommand;
        _featureService = featureService;
        _registrationEmailVerificationTokenDataFactory = registrationEmailVerificationTokenDataFactory;

        if (CoreHelpers.SettingHasValue(globalSettings.KdfDefaultHashKey))
        {
            _defaultKdfHmacKey = Encoding.UTF8.GetBytes(globalSettings.KdfDefaultHashKey);
        }
    }

    [HttpPost("register/send-verification-email")]
    public async Task<IActionResult> PostRegisterSendVerificationEmail([FromBody] RegisterSendVerificationEmailRequestModel model)
    {
        var token = await _sendVerificationEmailForRegistrationCommand.Run(model.Email, model.Name,
            model.ReceiveMarketingEmails);

        if (token != null)
        {
            return Ok(token);
        }

        return NoContent();
    }

    [HttpPost("register/verification-email-clicked")]
    public async Task<IActionResult> PostRegisterVerificationEmailClicked([FromBody] RegisterVerificationEmailClickedRequestModel model)
    {
        var tokenValid = RegistrationEmailVerificationTokenable.ValidateToken(_registrationEmailVerificationTokenDataFactory, model.EmailVerificationToken, model.Email);

        // Check to see if the user already exists - this is just to catch the unlikely but possible case
        // where a user finishes registration and then clicks the email verification link again.
        var user = await _userRepository.GetByEmailAsync(model.Email);
        var userExists = user != null;

        if (!tokenValid || userExists)
        {
            throw new BadRequestException("Expired link. Please restart registration or try logging in. You may already have an account");
        }

        return Ok();
    }

    [HttpPost("register/finish")]
    public async Task<RegisterFinishResponseModel> PostRegisterFinish([FromBody] RegisterFinishRequestModel model)
    {
        var user = model.ToUser();

        // Users will either have an emailed token or an email verification token - not both.
        IdentityResult identityResult = null;

        switch (model.GetTokenType())
        {
            case RegisterFinishTokenType.EmailVerification:
                identityResult =
                    await _registerUserCommand.RegisterUserViaEmailVerificationToken(user, model.MasterPasswordHash,
                        model.EmailVerificationToken);

                return ProcessRegistrationResult(identityResult, user);
            case RegisterFinishTokenType.OrganizationInvite:
                identityResult = await _registerUserCommand.RegisterUserViaOrganizationInviteToken(user, model.MasterPasswordHash,
                    model.OrgInviteToken, model.OrganizationUserId);

                return ProcessRegistrationResult(identityResult, user);
            case RegisterFinishTokenType.OrgSponsoredFreeFamilyPlan:
                identityResult = await _registerUserCommand.RegisterUserViaOrganizationSponsoredFreeFamilyPlanInviteToken(user, model.MasterPasswordHash, model.OrgSponsoredFreeFamilyPlanToken);

                return ProcessRegistrationResult(identityResult, user);
            case RegisterFinishTokenType.EmergencyAccessInvite:
                Debug.Assert(model.AcceptEmergencyAccessId.HasValue);
                identityResult = await _registerUserCommand.RegisterUserViaAcceptEmergencyAccessInviteToken(user, model.MasterPasswordHash,
                    model.AcceptEmergencyAccessInviteToken, model.AcceptEmergencyAccessId.Value);

                return ProcessRegistrationResult(identityResult, user);
            case RegisterFinishTokenType.ProviderInvite:
                Debug.Assert(model.ProviderUserId.HasValue);
                identityResult = await _registerUserCommand.RegisterUserViaProviderInviteToken(user, model.MasterPasswordHash,
                    model.ProviderInviteToken, model.ProviderUserId.Value);

                return ProcessRegistrationResult(identityResult, user);
            default:
                throw new BadRequestException("Invalid registration finish request");
        }
    }

    private RegisterFinishResponseModel ProcessRegistrationResult(IdentityResult result, User user)
    {
        if (result.Succeeded)
        {
            return new RegisterFinishResponseModel();
        }

        foreach (var error in result.Errors.Where(e => e.Code != "DuplicateUserName"))
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        throw new BadRequestException(ModelState);
    }

    [HttpPost("prelogin")]
    [Obsolete("Migrating to use a more descriptive endpoint that would support different types of prelogins. " +
              "Use prelogin/password instead. This endpoint has no EOL at the time of writing.")]
    public async Task<PasswordPreloginResponseModel> PostPrelogin([FromBody] PasswordPreloginRequestModel model)
    {
        // Same as PostPasswordPrelogin to maintain compatibility. Do not make changes in this function body,
        // only make changes in MakePasswordPreloginCall
        return await MakePasswordPreloginCall(model);
    }

    // There are two functions done this way because the open api docs that get generated in our build pipeline
    // cannot handle two of the same post attributes on the same function call. That is why there is a
    // PostPrelogin and the more appropriate PostPasswordPrelogin.
    [HttpPost("prelogin/password")]
    public async Task<PasswordPreloginResponseModel> PostPasswordPrelogin([FromBody] PasswordPreloginRequestModel model)
    {
        // Same as PostPrelogin to maintain backwards compatibility. Do not make changes in this function body,
        // only make changes in MakePasswordPreloginCall
        return await MakePasswordPreloginCall(model);
    }

    private async Task<PasswordPreloginResponseModel> MakePasswordPreloginCall(PasswordPreloginRequestModel model)
    {
        var kdfInformation = await _userRepository.GetKdfInformationByEmailAsync(model.Email);
        if (kdfInformation == null)
        {
            kdfInformation = GetDefaultKdf(model.Email);
        }
        return new PasswordPreloginResponseModel(kdfInformation, model.Email);
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

    private UserKdfInformation GetDefaultKdf(string email)
    {
        if (_defaultKdfHmacKey == null)
        {
            return _defaultKdfResults[0];
        }

        // Compute the HMAC hash of the email
        var hmacMessage = Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant());
        using var hmac = new System.Security.Cryptography.HMACSHA256(_defaultKdfHmacKey);
        var hmacHash = hmac.ComputeHash(hmacMessage);
        // Convert the hash to a number
        var hashHex = BitConverter.ToString(hmacHash).Replace("-", string.Empty).ToLowerInvariant();
        var hashFirst8Bytes = hashHex.Substring(0, 16);
        var hashNumber = long.Parse(hashFirst8Bytes, System.Globalization.NumberStyles.HexNumber);
        // Find the default KDF value for this hash number
        var hashIndex = (int)(Math.Abs(hashNumber) % _defaultKdfResults.Count);
        return _defaultKdfResults[hashIndex];
    }
}
