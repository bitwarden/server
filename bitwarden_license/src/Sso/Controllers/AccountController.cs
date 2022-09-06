using System.Security.Claims;
using Bit.Core;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Api;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Bit.Sso.Models;
using Bit.Sso.Utilities;
using IdentityModel;
using IdentityServer4;
using IdentityServer4.Extensions;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Sso.Controllers;

public class AccountController : Controller
{
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly IClientStore _clientStore;

    private readonly IIdentityServerInteractionService _interaction;
    private readonly ILogger<AccountController> _logger;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly ISsoUserRepository _ssoUserRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly IUserService _userService;
    private readonly II18nService _i18nService;
    private readonly UserManager<User> _userManager;
    private readonly IGlobalSettings _globalSettings;
    private readonly Core.Services.IEventService _eventService;
    private readonly IDataProtectorTokenFactory<SsoTokenable> _dataProtector;

    public AccountController(
        IAuthenticationSchemeProvider schemeProvider,
        IClientStore clientStore,
        IIdentityServerInteractionService interaction,
        ILogger<AccountController> logger,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService,
        ISsoConfigRepository ssoConfigRepository,
        ISsoUserRepository ssoUserRepository,
        IUserRepository userRepository,
        IPolicyRepository policyRepository,
        IUserService userService,
        II18nService i18nService,
        UserManager<User> userManager,
        IGlobalSettings globalSettings,
        Core.Services.IEventService eventService,
        IDataProtectorTokenFactory<SsoTokenable> dataProtector)
    {
        _schemeProvider = schemeProvider;
        _clientStore = clientStore;
        _interaction = interaction;
        _logger = logger;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
        _userRepository = userRepository;
        _ssoConfigRepository = ssoConfigRepository;
        _ssoUserRepository = ssoUserRepository;
        _policyRepository = policyRepository;
        _userService = userService;
        _i18nService = i18nService;
        _userManager = userManager;
        _eventService = eventService;
        _globalSettings = globalSettings;
        _dataProtector = dataProtector;
    }

    [HttpGet]
    public async Task<IActionResult> PreValidate(string domainHint)
    {
        try
        {
            // Validate domain_hint provided
            if (string.IsNullOrWhiteSpace(domainHint))
            {
                return InvalidJson("NoOrganizationIdentifierProvidedError");
            }

            // Validate organization exists from domain_hint
            var organization = await _organizationRepository.GetByIdentifierAsync(domainHint);
            if (organization == null)
            {
                return InvalidJson("OrganizationNotFoundByIdentifierError");
            }
            if (!organization.UseSso)
            {
                return InvalidJson("SsoNotAllowedForOrganizationError");
            }

            // Validate SsoConfig exists and is Enabled
            var ssoConfig = await _ssoConfigRepository.GetByIdentifierAsync(domainHint);
            if (ssoConfig == null)
            {
                return InvalidJson("SsoConfigurationNotFoundForOrganizationError");
            }
            if (!ssoConfig.Enabled)
            {
                return InvalidJson("SsoNotEnabledForOrganizationError");
            }

            // Validate Authentication Scheme exists and is loaded (cache)
            var scheme = await _schemeProvider.GetSchemeAsync(organization.Id.ToString());
            if (scheme == null || !(scheme is IDynamicAuthenticationScheme dynamicScheme))
            {
                return InvalidJson("NoSchemeOrHandlerForSsoConfigurationFoundError");
            }

            // Run scheme validation
            try
            {
                await dynamicScheme.Validate();
            }
            catch (Exception ex)
            {
                var translatedException = _i18nService.GetLocalizedHtmlString(ex.Message);
                var errorKey = "InvalidSchemeConfigurationError";
                if (!translatedException.ResourceNotFound)
                {
                    errorKey = ex.Message;
                }
                return InvalidJson(errorKey, translatedException.ResourceNotFound ? ex : null);
            }

            var tokenable = new SsoTokenable(organization, _globalSettings.Sso.SsoTokenLifetimeInSeconds);
            var token = _dataProtector.Protect(tokenable);

            return new SsoPreValidateResponseModel(token);
        }
        catch (Exception ex)
        {
            return InvalidJson("PreValidationError", ex);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Login(string returnUrl)
    {
        var context = await _interaction.GetAuthorizationContextAsync(returnUrl);

        if (!context.Parameters.AllKeys.Contains("domain_hint") ||
            string.IsNullOrWhiteSpace(context.Parameters["domain_hint"]))
        {
            throw new Exception(_i18nService.T("NoDomainHintProvided"));
        }

        var ssoToken = context.Parameters[SsoTokenable.TokenIdentifier];

        if (string.IsNullOrWhiteSpace(ssoToken))
        {
            return Unauthorized("A valid SSO token is required to continue with SSO login");
        }

        var domainHint = context.Parameters["domain_hint"];
        var organization = await _organizationRepository.GetByIdentifierAsync(domainHint);

        if (organization == null)
        {
            return InvalidJson("OrganizationNotFoundByIdentifierError");
        }

        var tokenable = _dataProtector.Unprotect(ssoToken);

        if (!tokenable.TokenIsValid(organization))
        {
            return Unauthorized("The SSO token associated with your request is expired. A valid SSO token is required to continue.");
        }

        return RedirectToAction(nameof(ExternalChallenge), new
        {
            scheme = organization.Id.ToString(),
            returnUrl,
            state = context.Parameters["state"],
            userIdentifier = context.Parameters["session_state"],
        });
    }

    [HttpGet]
    public IActionResult ExternalChallenge(string scheme, string returnUrl, string state, string userIdentifier)
    {
        if (string.IsNullOrEmpty(returnUrl))
        {
            returnUrl = "~/";
        }

        if (!Url.IsLocalUrl(returnUrl) && !_interaction.IsValidReturnUrl(returnUrl))
        {
            throw new Exception(_i18nService.T("InvalidReturnUrl"));
        }

        var props = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(ExternalCallback)),
            Items =
            {
                // scheme will get serialized into `State` and returned back
                { "scheme", scheme },
                { "return_url", returnUrl },
                { "state", state },
                { "user_identifier", userIdentifier },
            }
        };

        return Challenge(props, scheme);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalCallback()
    {
        // Read external identity from the temporary cookie
        var result = await HttpContext.AuthenticateAsync(
            AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme);
        if (result?.Succeeded != true)
        {
            throw new Exception(_i18nService.T("ExternalAuthenticationError"));
        }

        // Debugging
        var externalClaims = result.Principal.Claims.Select(c => $"{c.Type}: {c.Value}");
        _logger.LogDebug("External claims: {@claims}", externalClaims);

        // Lookup our user and external provider info
        var (user, provider, providerUserId, claims, ssoConfigData) = await FindUserFromExternalProviderAsync(result);
        if (user == null)
        {
            // This might be where you might initiate a custom workflow for user registration
            // in this sample we don't show how that would be done, as our sample implementation
            // simply auto-provisions new external user
            var userIdentifier = result.Properties.Items.Keys.Contains("user_identifier") ?
                result.Properties.Items["user_identifier"] : null;
            user = await AutoProvisionUserAsync(provider, providerUserId, claims, userIdentifier, ssoConfigData);
        }

        if (user != null)
        {
            // This allows us to collect any additional claims or properties
            // for the specific protocols used and store them in the local auth cookie.
            // this is typically used to store data needed for signout from those protocols.
            var additionalLocalClaims = new List<Claim>();
            var localSignInProps = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(1)
            };
            ProcessLoginCallback(result, additionalLocalClaims, localSignInProps);

            // Issue authentication cookie for user
            await HttpContext.SignInAsync(new IdentityServerUser(user.Id.ToString())
            {
                DisplayName = user.Email,
                IdentityProvider = provider,
                AdditionalClaims = additionalLocalClaims.ToArray()
            }, localSignInProps);
        }

        // Delete temporary cookie used during external authentication
        await HttpContext.SignOutAsync(AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme);

        // Retrieve return URL
        var returnUrl = result.Properties.Items["return_url"] ?? "~/";

        // Check if external login is in the context of an OIDC request
        var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
        if (context != null)
        {
            if (IsNativeClient(context))
            {
                // The client is native, so this change in how to
                // return the response is for better UX for the end user.
                HttpContext.Response.StatusCode = 200;
                HttpContext.Response.Headers["Location"] = string.Empty;
                return View("Redirect", new RedirectViewModel { RedirectUrl = returnUrl });
            }
        }

        return Redirect(returnUrl);
    }

    [HttpGet]
    public async Task<IActionResult> Logout(string logoutId)
    {
        // Build a model so the logged out page knows what to display
        var (updatedLogoutId, redirectUri, externalAuthenticationScheme) = await GetLoggedOutDataAsync(logoutId);

        if (User?.Identity.IsAuthenticated == true)
        {
            // Delete local authentication cookie
            await HttpContext.SignOutAsync();
        }

        // HACK: Temporary workaroud for the time being that doesn't try to sign out of OneLogin schemes,
        // which doesnt support SLO
        if (externalAuthenticationScheme != null && !externalAuthenticationScheme.Contains("onelogin"))
        {
            // Build a return URL so the upstream provider will redirect back
            // to us after the user has logged out. this allows us to then
            // complete our single sign-out processing.
            var url = Url.Action("Logout", new { logoutId = updatedLogoutId });

            // This triggers a redirect to the external provider for sign-out
            return SignOut(new AuthenticationProperties { RedirectUri = url }, externalAuthenticationScheme);
        }
        if (redirectUri != null)
        {
            return View("Redirect", new RedirectViewModel { RedirectUrl = redirectUri });
        }
        else
        {
            return Redirect("~/");
        }
    }

    private async Task<(User user, string provider, string providerUserId, IEnumerable<Claim> claims, SsoConfigurationData config)>
        FindUserFromExternalProviderAsync(AuthenticateResult result)
    {
        var provider = result.Properties.Items["scheme"];
        var orgId = new Guid(provider);
        var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(orgId);
        if (ssoConfig == null || !ssoConfig.Enabled)
        {
            throw new Exception(_i18nService.T("OrganizationOrSsoConfigNotFound"));
        }

        var ssoConfigData = ssoConfig.GetData();
        var externalUser = result.Principal;

        // Validate acr claim against expectation before going further
        if (!string.IsNullOrWhiteSpace(ssoConfigData.ExpectedReturnAcrValue))
        {
            var acrClaim = externalUser.FindFirst(JwtClaimTypes.AuthenticationContextClassReference);
            if (acrClaim?.Value != ssoConfigData.ExpectedReturnAcrValue)
            {
                throw new Exception(_i18nService.T("AcrMissingOrInvalid"));
            }
        }

        // Ensure the NameIdentifier used is not a transient name ID, if so, we need a different attribute
        //  for the user identifier.
        static bool nameIdIsNotTransient(Claim c) => c.Type == ClaimTypes.NameIdentifier
            && (c.Properties == null
            || !c.Properties.ContainsKey(SamlPropertyKeys.ClaimFormat)
            || c.Properties[SamlPropertyKeys.ClaimFormat] != SamlNameIdFormats.Transient);

        // Try to determine the unique id of the external user (issued by the provider)
        // the most common claim type for that are the sub claim and the NameIdentifier
        // depending on the external provider, some other claim type might be used
        var customUserIdClaimTypes = ssoConfigData.GetAdditionalUserIdClaimTypes();
        var userIdClaim = externalUser.FindFirst(c => customUserIdClaimTypes.Contains(c.Type)) ??
                          externalUser.FindFirst(JwtClaimTypes.Subject) ??
                          externalUser.FindFirst(nameIdIsNotTransient) ??
                          // Some SAML providers may use the `uid` attribute for this
                          //    where a transient NameID has been sent in the subject
                          externalUser.FindFirst("uid") ??
                          externalUser.FindFirst("upn") ??
                          externalUser.FindFirst("eppn") ??
                          throw new Exception(_i18nService.T("UnknownUserId"));

        // Remove the user id claim so we don't include it as an extra claim if/when we provision the user
        var claims = externalUser.Claims.ToList();
        claims.Remove(userIdClaim);

        // find external user
        var providerUserId = userIdClaim.Value;

        var user = await _userRepository.GetBySsoUserAsync(providerUserId, orgId);

        return (user, provider, providerUserId, claims, ssoConfigData);
    }

    private async Task<User> AutoProvisionUserAsync(string provider, string providerUserId,
        IEnumerable<Claim> claims, string userIdentifier, SsoConfigurationData config)
    {
        var name = GetName(claims, config.GetAdditionalNameClaimTypes());
        var email = GetEmailAddress(claims, config.GetAdditionalEmailClaimTypes());
        if (string.IsNullOrWhiteSpace(email) && providerUserId.Contains("@"))
        {
            email = providerUserId;
        }

        if (!Guid.TryParse(provider, out var orgId))
        {
            // TODO: support non-org (server-wide) SSO in the future?
            throw new Exception(_i18nService.T("SSOProviderIsNotAnOrgId", provider));
        }

        User existingUser = null;
        if (string.IsNullOrWhiteSpace(userIdentifier))
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new Exception(_i18nService.T("CannotFindEmailClaim"));
            }
            existingUser = await _userRepository.GetByEmailAsync(email);
        }
        else
        {
            var split = userIdentifier.Split(",");
            if (split.Length < 2)
            {
                throw new Exception(_i18nService.T("InvalidUserIdentifier"));
            }
            var userId = split[0];
            var token = split[1];

            var tokenOptions = new TokenOptions();

            var claimedUser = await _userService.GetUserByIdAsync(userId);
            if (claimedUser != null)
            {
                var tokenIsValid = await _userManager.VerifyUserTokenAsync(
                    claimedUser, tokenOptions.PasswordResetTokenProvider, TokenPurposes.LinkSso, token);
                if (tokenIsValid)
                {
                    existingUser = claimedUser;
                }
                else
                {
                    throw new Exception(_i18nService.T("UserIdAndTokenMismatch"));
                }
            }
        }

        OrganizationUser orgUser = null;
        var organization = await _organizationRepository.GetByIdAsync(orgId);
        if (organization == null)
        {
            throw new Exception(_i18nService.T("CouldNotFindOrganization", orgId));
        }

        // Try to find OrgUser via existing User Id (accepted/confirmed user)
        if (existingUser != null)
        {
            var orgUsersByUserId = await _organizationUserRepository.GetManyByUserAsync(existingUser.Id);
            orgUser = orgUsersByUserId.SingleOrDefault(u => u.OrganizationId == orgId);
        }

        // If no Org User found by Existing User Id - search all organization users via email
        orgUser ??= await _organizationUserRepository.GetByOrganizationEmailAsync(orgId, email);

        // All Existing User flows handled below
        if (existingUser != null)
        {
            if (existingUser.UsesKeyConnector &&
                (orgUser == null || orgUser.Status == OrganizationUserStatusType.Invited))
            {
                throw new Exception(_i18nService.T("UserAlreadyExistsKeyConnector"));
            }

            if (orgUser == null)
            {
                // Org User is not created - no invite has been sent
                throw new Exception(_i18nService.T("UserAlreadyExistsInviteProcess"));
            }

            if (orgUser.Status == OrganizationUserStatusType.Invited)
            {
                // Org User is invited - they must manually accept the invite via email and authenticate with MP
                throw new Exception(_i18nService.T("UserAlreadyInvited", email, organization.Name));
            }

            // Accepted or Confirmed - create SSO link and return;
            await CreateSsoUserRecord(providerUserId, existingUser.Id, orgId, orgUser);
            return existingUser;
        }

        // Before any user creation - if Org User doesn't exist at this point - make sure there are enough seats to add one
        if (orgUser == null && organization.Seats.HasValue)
        {
            var occupiedSeats = await _organizationService.GetOccupiedSeatCount(organization);
            var initialSeatCount = organization.Seats.Value;
            var availableSeats = initialSeatCount - occupiedSeats;
            var prorationDate = DateTime.UtcNow;
            if (availableSeats < 1)
            {
                try
                {
                    if (_globalSettings.SelfHosted)
                    {
                        throw new Exception("Cannot autoscale on self-hosted instance.");
                    }

                    await _organizationService.AutoAddSeatsAsync(organization, 1, prorationDate);
                }
                catch (Exception e)
                {
                    if (organization.Seats.Value != initialSeatCount)
                    {
                        await _organizationService.AdjustSeatsAsync(orgId, initialSeatCount - organization.Seats.Value, prorationDate);
                    }
                    _logger.LogInformation(e, "SSO auto provisioning failed");
                    throw new Exception(_i18nService.T("NoSeatsAvailable", organization.Name));
                }
            }
        }

        // Create user record - all existing user flows are handled above
        var user = new User
        {
            Name = name,
            Email = email,
            ApiKey = CoreHelpers.SecureRandomString(30)
        };
        await _userService.RegisterUserAsync(user);

        // If the organization has 2fa policy enabled, make sure to default jit user 2fa to email
        var twoFactorPolicy =
            await _policyRepository.GetByOrganizationIdTypeAsync(orgId, PolicyType.TwoFactorAuthentication);
        if (twoFactorPolicy != null && twoFactorPolicy.Enabled)
        {
            user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
            {
                [TwoFactorProviderType.Email] = new TwoFactorProvider
                {
                    MetaData = new Dictionary<string, object> { ["Email"] = user.Email.ToLowerInvariant() },
                    Enabled = true
                }
            });
            await _userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Email);
        }

        // Create Org User if null or else update existing Org User
        if (orgUser == null)
        {
            orgUser = new OrganizationUser
            {
                OrganizationId = orgId,
                UserId = user.Id,
                Type = OrganizationUserType.User,
                Status = OrganizationUserStatusType.Invited
            };
            await _organizationUserRepository.CreateAsync(orgUser);
        }
        else
        {
            orgUser.UserId = user.Id;
            await _organizationUserRepository.ReplaceAsync(orgUser);
        }

        // Create sso user record
        await CreateSsoUserRecord(providerUserId, user.Id, orgId, orgUser);

        return user;
    }

    private IActionResult InvalidJson(string errorMessageKey, Exception ex = null)
    {
        Response.StatusCode = ex == null ? 400 : 500;
        return Json(new ErrorResponseModel(_i18nService.T(errorMessageKey))
        {
            ExceptionMessage = ex?.Message,
            ExceptionStackTrace = ex?.StackTrace,
            InnerExceptionMessage = ex?.InnerException?.Message,
        });
    }

    private string GetEmailAddress(IEnumerable<Claim> claims, IEnumerable<string> additionalClaimTypes)
    {
        var filteredClaims = claims.Where(c => !string.IsNullOrWhiteSpace(c.Value) && c.Value.Contains("@"));

        var email = filteredClaims.GetFirstMatch(additionalClaimTypes.ToArray()) ??
            filteredClaims.GetFirstMatch(JwtClaimTypes.Email, ClaimTypes.Email,
                SamlClaimTypes.Email, "mail", "emailaddress");
        if (!string.IsNullOrWhiteSpace(email))
        {
            return email;
        }

        var username = filteredClaims.GetFirstMatch(JwtClaimTypes.PreferredUserName,
            SamlClaimTypes.UserId, "uid");
        if (!string.IsNullOrWhiteSpace(username))
        {
            return username;
        }

        return null;
    }

    private string GetName(IEnumerable<Claim> claims, IEnumerable<string> additionalClaimTypes)
    {
        var filteredClaims = claims.Where(c => !string.IsNullOrWhiteSpace(c.Value));

        var name = filteredClaims.GetFirstMatch(additionalClaimTypes.ToArray()) ??
            filteredClaims.GetFirstMatch(JwtClaimTypes.Name, ClaimTypes.Name,
                SamlClaimTypes.DisplayName, SamlClaimTypes.CommonName, "displayname", "cn");
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var givenName = filteredClaims.GetFirstMatch(SamlClaimTypes.GivenName, "givenname", "firstname",
            "fn", "fname", "nickname");
        var surname = filteredClaims.GetFirstMatch(SamlClaimTypes.Surname, "sn", "surname", "lastname");
        var nameParts = new[] { givenName, surname }.Where(p => !string.IsNullOrWhiteSpace(p));
        if (nameParts.Any())
        {
            return string.Join(' ', nameParts);
        }

        return null;
    }

    private async Task CreateSsoUserRecord(string providerUserId, Guid userId, Guid orgId, OrganizationUser orgUser)
    {
        // Delete existing SsoUser (if any) - avoids error if providerId has changed and the sso link is stale
        var existingSsoUser = await _ssoUserRepository.GetByUserIdOrganizationIdAsync(orgId, userId);
        if (existingSsoUser != null)
        {
            await _ssoUserRepository.DeleteAsync(userId, orgId);
            await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_ResetSsoLink);
        }
        else
        {
            // If no stale user, this is the user's first Sso login ever
            await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_FirstSsoLogin);
        }

        var ssoUser = new SsoUser
        {
            ExternalId = providerUserId,
            UserId = userId,
            OrganizationId = orgId,
        };
        await _ssoUserRepository.CreateAsync(ssoUser);
    }

    private void ProcessLoginCallback(AuthenticateResult externalResult,
        List<Claim> localClaims, AuthenticationProperties localSignInProps)
    {
        // If the external system sent a session id claim, copy it over
        // so we can use it for single sign-out
        var sid = externalResult.Principal.Claims.FirstOrDefault(x => x.Type == JwtClaimTypes.SessionId);
        if (sid != null)
        {
            localClaims.Add(new Claim(JwtClaimTypes.SessionId, sid.Value));
        }

        // If the external provider issued an idToken, we'll keep it for signout
        var idToken = externalResult.Properties.GetTokenValue("id_token");
        if (idToken != null)
        {
            localSignInProps.StoreTokens(
                new[] { new AuthenticationToken { Name = "id_token", Value = idToken } });
        }
    }

    private async Task<string> GetProviderAsync(string returnUrl)
    {
        var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
        if (context?.IdP != null && await _schemeProvider.GetSchemeAsync(context.IdP) != null)
        {
            return context.IdP;
        }
        var schemes = await _schemeProvider.GetAllSchemesAsync();
        var providers = schemes.Select(x => x.Name).ToList();
        return providers.FirstOrDefault();
    }

    private async Task<(string, string, string)> GetLoggedOutDataAsync(string logoutId)
    {
        // Get context information (client name, post logout redirect URI and iframe for federated signout)
        var logout = await _interaction.GetLogoutContextAsync(logoutId);
        string externalAuthenticationScheme = null;
        if (User?.Identity.IsAuthenticated == true)
        {
            var idp = User.FindFirst(JwtClaimTypes.IdentityProvider)?.Value;
            if (idp != null && idp != IdentityServerConstants.LocalIdentityProvider)
            {
                var providerSupportsSignout = await HttpContext.GetSchemeSupportsSignOutAsync(idp);
                if (providerSupportsSignout)
                {
                    if (logoutId == null)
                    {
                        // If there's no current logout context, we need to create one
                        // this captures necessary info from the current logged in user
                        // before we signout and redirect away to the external IdP for signout
                        logoutId = await _interaction.CreateLogoutContextAsync();
                    }

                    externalAuthenticationScheme = idp;
                }
            }
        }

        return (logoutId, logout?.PostLogoutRedirectUri, externalAuthenticationScheme);
    }

    public bool IsNativeClient(IdentityServer4.Models.AuthorizationRequest context)
    {
        return !context.RedirectUri.StartsWith("https", StringComparison.Ordinal)
           && !context.RedirectUri.StartsWith("http", StringComparison.Ordinal);
    }
}
