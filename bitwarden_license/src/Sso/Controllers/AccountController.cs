using System.Security.Claims;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.Registration;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Bit.Sso.Models;
using Bit.Sso.Utilities;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AuthenticationSchemes = Bit.Core.AuthenticationSchemes;
using DIM = Duende.IdentityServer.Models;

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
    private readonly IOrganizationDomainRepository _organizationDomainRepository;
    private readonly IRegisterUserCommand _registerUserCommand;
    private readonly IFeatureService _featureService;

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
        IDataProtectorTokenFactory<SsoTokenable> dataProtector,
        IOrganizationDomainRepository organizationDomainRepository,
        IRegisterUserCommand registerUserCommand,
        IFeatureService featureService)
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
        _organizationDomainRepository = organizationDomainRepository;
        _registerUserCommand = registerUserCommand;
        _featureService = featureService;
    }

    [HttpGet]
    public async Task<IActionResult> PreValidateAsync(string domainHint)
    {
        try
        {
            // Validate domain_hint provided
            if (string.IsNullOrWhiteSpace(domainHint))
            {
                _logger.LogError(new ArgumentException("domainHint is required."), "domainHint not specified.");
                return InvalidJson("SsoInvalidIdentifierError");
            }

            // Validate organization exists from domain_hint
            var organization = await _organizationRepository.GetByIdentifierAsync(domainHint);
            if (organization is not { UseSso: true })
            {
                _logger.LogError("Organization not configured to use SSO.");
                return InvalidJson("SsoInvalidIdentifierError");
            }

            // Validate SsoConfig exists and is Enabled
            var ssoConfig = await _ssoConfigRepository.GetByIdentifierAsync(domainHint);
            if (ssoConfig is not { Enabled: true })
            {
                _logger.LogError("SsoConfig not enabled.");
                return InvalidJson("SsoInvalidIdentifierError");
            }

            // Validate Authentication Scheme exists and is loaded (cache)
            var scheme = await _schemeProvider.GetSchemeAsync(organization.Id.ToString());
            if (scheme is not IDynamicAuthenticationScheme dynamicScheme)
            {
                _logger.LogError("Invalid authentication scheme for organization.");
                return InvalidJson("SsoInvalidIdentifierError");
            }

            // Run scheme validation
            try
            {
                await dynamicScheme.Validate();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while validating SSO dynamic scheme.");
                return InvalidJson("SsoInvalidIdentifierError");
            }

            var tokenable = new SsoTokenable(organization, _globalSettings.Sso.SsoTokenLifetimeInSeconds);
            var token = _dataProtector.Protect(tokenable);

            return new SsoPreValidateResponseModel(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during SSO prevalidation.");
            return InvalidJson("SsoInvalidIdentifierError");
        }
    }

    [HttpGet]
    public async Task<IActionResult> LoginAsync(string returnUrl)
    {
        var context = await _interaction.GetAuthorizationContextAsync(returnUrl);

#nullable disable
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
#nullable restore

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

        // Clean the returnUrl
        returnUrl = CoreHelpers.ReplaceWhiteSpace(returnUrl, string.Empty);
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
        // Feature flag (PM-24579): Prevent SSO on existing non-compliant users.
        var preventOrgUserLoginIfStatusInvalid =
            _featureService.IsEnabled(FeatureFlagKeys.PM24579_PreventSsoOnExistingNonCompliantUsers);

        // Read external identity from the temporary cookie
        var result = await HttpContext.AuthenticateAsync(
            AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme);

        if (preventOrgUserLoginIfStatusInvalid)
        {
            if (!result.Succeeded)
            {
                throw new Exception(_i18nService.T("ExternalAuthenticationError"));
            }
        }
        else
        {
            if (result?.Succeeded != true)
            {
                throw new Exception(_i18nService.T("ExternalAuthenticationError"));
            }
        }

        // See if the user has logged in with this SSO provider before and has already been provisioned.
        // This is signified by the user existing in the User table and the SSOUser table for the SSO provider they're using.
        var (possibleUserLinkedWithSso, provider, providerUserId, claims, ssoConfigData) = await FindUserFromExternalProviderAsync(result);

        // We will look these up as required (lazy resolution) to avoid multiple DB hits.
        Organization? organization = null;
        OrganizationUser? orgUser = null;

        // The user has not authenticated with this SSO provider before.
        // They could have an existing Bitwarden account in the User table though.
        if (possibleUserLinkedWithSso == null)
        {
#nullable disable
            // If we're manually linking to SSO, the user's external identifier will be passed as query string parameter.
            var userIdentifier = result.Properties.Items.Keys.Contains("user_identifier")
                ? result.Properties.Items["user_identifier"]
                : null;

            var (provisionedUser, foundOrganization, foundOrCreatedOrgUser) =
                await CreateUserAndOrgUserConditionallyAsync(
                    provider,
                    providerUserId,
                    claims,
                    userIdentifier,
                    ssoConfigData);
#nullable restore

            possibleUserLinkedWithSso = provisionedUser;

            if (preventOrgUserLoginIfStatusInvalid)
            {
                organization = foundOrganization;
                orgUser = foundOrCreatedOrgUser;
            }
        }

        if (preventOrgUserLoginIfStatusInvalid)
        {
            if (possibleUserLinkedWithSso == null) throw new Exception(_i18nService.T("UserShouldBeFound"));
            User guaranteedSsoUser = possibleUserLinkedWithSso;

            await PreventOrgUserLoginIfStatusInvalidAsync(organization, provider, orgUser, guaranteedSsoUser);

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
            await HttpContext.SignInAsync(
                new IdentityServerUser(guaranteedSsoUser.Id.ToString())
                {
                    DisplayName = guaranteedSsoUser.Email,
                    IdentityProvider = provider,
                    AdditionalClaims = additionalLocalClaims.ToArray()
                }, localSignInProps);
        }
        else
        {
            // Either the user already authenticated with the SSO provider, or we've just provisioned them.
            // Either way, we have associated the SSO login with a Bitwarden user.
            // We will now sign the Bitwarden user in.
            if (possibleUserLinkedWithSso != null)
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
                await HttpContext.SignInAsync(
                    new IdentityServerUser(possibleUserLinkedWithSso.Id.ToString())
                    {
                        DisplayName = possibleUserLinkedWithSso.Email,
                        IdentityProvider = provider,
                        AdditionalClaims = additionalLocalClaims.ToArray()
                    }, localSignInProps);
            }
        }

        // Delete temporary cookie used during external authentication
        await HttpContext.SignOutAsync(AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme);

#nullable disable
        // Retrieve return URL
        var returnUrl = result.Properties.Items["return_url"] ?? "~/";
#nullable restore

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

#nullable disable
    [HttpGet]
    public async Task<IActionResult> LogoutAsync(string logoutId)
    {
        // Build a model so the logged out page knows what to display
        var (updatedLogoutId, redirectUri, externalAuthenticationScheme) = await GetLoggedOutDataAsync(logoutId);

        if (User?.Identity.IsAuthenticated == true)
        {
            // Delete local authentication cookie
            await HttpContext.SignOutAsync();
        }

        // HACK: Temporary workaround for the time being that doesn't try to sign out of OneLogin schemes,
        // which doesn't support SLO
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
#nullable restore

    /// <summary>
    /// Attempts to map the external identity to a Bitwarden user, through the SsoUser table, which holds the `externalId`.
    /// The claims on the external identity are used to determine an `externalId`, and that is used to find the appropriate `SsoUser` and `User` records.
    /// </summary>
    private async Task<(
        User? possibleSsoUser,
        string provider,
        string providerUserId,
        IEnumerable<Claim> claims,
        SsoConfigurationData config
    )> FindUserFromExternalProviderAsync(AuthenticateResult result)
    {
#nullable disable
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
                                                         || !c.Properties.TryGetValue(SamlPropertyKeys.ClaimFormat,
                                                             out var claimFormat)
                                                         || claimFormat != SamlNameIdFormats.Transient);

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
#nullable restore

        // Remove the user id claim so we don't include it as an extra claim if/when we provision the user
        var claims = externalUser.Claims.ToList();
        claims.Remove(userIdClaim);

        // find external user
        var providerUserId = userIdClaim.Value;

        var possibleSsoUser = await _userRepository.GetBySsoUserAsync(providerUserId, orgId);

        return (possibleSsoUser, provider, providerUserId, claims, ssoConfigData);
    }

    /// <summary>
    /// This function seeks to set up the org user record or create a new user record based on the conditions
    /// below.
    ///
    /// This handles three different scenarios:
    /// 1. Creating an SsoUser link for an existing User and OrganizationUser
    ///     - User is a member of the organization, but hasn't authenticated with the org's SSO provider before.
    /// 2. Creating a new User and a new OrganizationUser, then establishing an SsoUser link
    ///     - User is joining the organization through JIT provisioning, without a pending invitation
    /// 3. Creating a new User for an existing OrganizationUser (created by invitation), then establishing an SsoUser link
    ///     - User is signing in with a pending invitation.
    /// </summary>
    /// <param name="provider">The external identity provider.</param>
    /// <param name="providerUserId">The external identity provider's user identifier.</param>
    /// <param name="claims">The claims from the external IdP.</param>
    /// <param name="userIdentifier">The user identifier used for manual SSO linking.</param>
    /// <param name="ssoConfigData">The SSO configuration for the organization.</param>
    /// <returns>Guaranteed to return the user to sign in as well as the found organization and org user.</returns>
    /// <exception cref="Exception">An exception if the user cannot be provisioned as requested.</exception>
    private async Task<(User user, Organization foundOrganization, OrganizationUser foundOrgUser)>
        CreateUserAndOrgUserConditionallyAsync(
            string provider,
            string providerUserId,
            IEnumerable<Claim> claims,
            string userIdentifier,
            SsoConfigurationData ssoConfigData
        )
    {
        // Try to get the email from the claims as we don't know if we have a user record yet.
        var name = GetName(claims, ssoConfigData.GetAdditionalNameClaimTypes());
        var email = TryGetEmailAddress(claims, ssoConfigData, providerUserId);

        User? possibleExisingUser;
        if (string.IsNullOrWhiteSpace(userIdentifier))
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new Exception(_i18nService.T("CannotFindEmailClaim"));
            }

            possibleExisingUser = await _userRepository.GetByEmailAsync(email);
        }
        else
        {
            possibleExisingUser = await GetUserFromManualLinkingDataAsync(userIdentifier);
        }

        // Find the org (we error if we can't find an org because no org is not valid)
        var organization = await GetOrganizationByProviderAsync(provider);

        // Try to find an org user (null org user possible and valid here)
        var possibleOrgUser = await GetOrganizationUserByUserAndOrgOrEmailAsync(possibleExisingUser, organization.Id, email);

        //----------------------------------------------------
        // Scenario 1: We've found the user in the User table
        //----------------------------------------------------
        if (possibleExisingUser != null)
        {
            if (possibleExisingUser.UsesKeyConnector &&
                (possibleOrgUser == null || possibleOrgUser.Status == OrganizationUserStatusType.Invited))
            {
                throw new Exception(_i18nService.T("UserAlreadyExistsKeyConnector"));
            }

            if (possibleOrgUser == null)
            {
                // Org User is not created - no invite has been sent
                throw new Exception(_i18nService.T("UserAlreadyExistsInviteProcess"));
            }

            /*
             * ----------------------------------------------------
             *              Critical Code Check Here
             *
             * We want to ensure that a user is not in the Invited
             * state explicitly because if we don't confirm this
             * here we have an authentication bypass.
             * ----------------------------------------------------
             */
            EnsureUserNotInInvited(possibleOrgUser.Status, organization.DisplayName());

            // If the user already exists in Bitwarden, we require that the user already be in the org,
            // and that they are either Accepted or Confirmed.
            WhiteListedOrgUserStatus(
                possibleOrgUser.Status,
                allowedStatuses: [
                    OrganizationUserStatusType.Accepted,
                    OrganizationUserStatusType.Confirmed
                ],
                organization.DisplayName());

            // Since we're in the auto-provisioning logic, this means that the user exists, but they have not
            // authenticated with the org's SSO provider before now (otherwise we wouldn't be auto-provisioning them).
            // We've verified that the user is Accepted or Confnirmed, so we can create an SsoUser link and proceed
            // with authentication.
            await CreateSsoUserRecordAsync(providerUserId, possibleExisingUser.Id, organization.Id, possibleOrgUser);

            return (possibleExisingUser, organization, possibleOrgUser);
        }

        // Before any user creation - if Org User doesn't exist at this point - make sure there are enough seats to add one
        if (possibleOrgUser == null && organization.Seats.HasValue)
        {
            var occupiedSeats =
                await _organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
            var initialSeatCount = organization.Seats.Value;
            var availableSeats = initialSeatCount - occupiedSeats.Total;
            if (availableSeats < 1)
            {
                try
                {
                    if (_globalSettings.SelfHosted)
                    {
                        throw new Exception("Cannot autoscale on self-hosted instance.");
                    }

                    await _organizationService.AutoAddSeatsAsync(organization, 1);
                }
                catch (Exception e)
                {
                    if (organization.Seats.Value != initialSeatCount)
                    {
                        await _organizationService.AdjustSeatsAsync(organization.Id,
                            initialSeatCount - organization.Seats.Value);
                    }

                    _logger.LogInformation(e, "SSO auto provisioning failed");
                    throw new Exception(_i18nService.T("NoSeatsAvailable", organization.DisplayName()));
                }
            }
        }

        // If the email domain is verified, we can mark the email as verified
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new Exception(_i18nService.T("CannotFindEmailClaim"));
        }

        var emailVerified = false;
        var emailDomain = CoreHelpers.GetEmailDomain(email);
        if (!string.IsNullOrWhiteSpace(emailDomain))
        {
            var organizationDomain =
                await _organizationDomainRepository.GetDomainByOrgIdAndDomainNameAsync(organization.Id, emailDomain);
            emailVerified = organizationDomain?.VerifiedDate.HasValue ?? false;
        }

        //--------------------------------------------------
        // Scenarios 2 and 3: We need to register a new user
        //--------------------------------------------------
        var user = new User
        {
            Name = name,
            Email = email,
            EmailVerified = emailVerified,
            ApiKey = CoreHelpers.SecureRandomString(30)
        };
        await _registerUserCommand.RegisterUser(user);

        // If the organization has 2fa policy enabled, make sure to default jit user 2fa to email
        var twoFactorPolicy =
            await _policyRepository.GetByOrganizationIdTypeAsync(organization.Id, PolicyType.TwoFactorAuthentication);
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

        //-----------------------------------------------------------------
        // Scenario 2: We also need to create an OrganizationUser
        // This means that an invitation was not sent for this user and we
        // need to establish their invited status now.
        //-----------------------------------------------------------------
        if (possibleOrgUser == null)
        {
            possibleOrgUser = new OrganizationUser
            {
                OrganizationId = organization.Id,
                UserId = user.Id,
                Type = OrganizationUserType.User,
                Status = OrganizationUserStatusType.Invited
            };
            await _organizationUserRepository.CreateAsync(possibleOrgUser);
        }

        //-----------------------------------------------------------------
        // Scenario 3: There is already an existing OrganizationUser
        // That was established through an invitation. We just need to
        // update the UserId now that we have created a User record.
        //-----------------------------------------------------------------
        else
        {
            possibleOrgUser.UserId = user.Id;
            await _organizationUserRepository.ReplaceAsync(possibleOrgUser);
        }

        // Create the SsoUser record to link the user to the SSO provider.
        await CreateSsoUserRecordAsync(providerUserId, user.Id, organization.Id, possibleOrgUser);

        return (user, organization, possibleOrgUser);
    }

    /// <summary>
    /// Validates an organization user is allowed to log in via SSO and blocks invalid statuses.
    /// Lazily resolves the organization and organization user if not provided.
    /// </summary>
    /// <param name="organization">The target organization; if null, resolved from provider.</param>
    /// <param name="provider">The SSO scheme provider value (organization id as a GUID string).</param>
    /// <param name="orgUser">The organization-user record; if null, looked up by user/org or user email for invited users.</param>
    /// <param name="user">The user attempting to sign in (existing or newly provisioned).</param>
    /// <exception cref="Exception">Thrown if the organization cannot be resolved from provider;
    /// the organization user cannot be found; or the organization user status is not allowed.</exception>
    private async Task PreventOrgUserLoginIfStatusInvalidAsync(
        Organization? organization,
        string provider,
        OrganizationUser? orgUser,
        User user)
    {
        // Lazily get organization if not already known
        organization ??= await GetOrganizationByProviderAsync(provider);

        // Lazily get the org user if not already known
        orgUser ??= await GetOrganizationUserByUserAndOrgOrEmailAsync(
            user,
            organization.Id,
            user.Email);

        if (orgUser != null)
        {
            // Invited is allowed at this point because we know the user is trying to accept an org invite.
            WhiteListedOrgUserStatus(
                orgUser.Status,
                allowedStatuses: [
                    OrganizationUserStatusType.Invited,
                    OrganizationUserStatusType.Accepted,
                    OrganizationUserStatusType.Confirmed,
                ],
                organization.DisplayName());
        }
        else
        {
            throw new Exception(_i18nService.T("CouldNotFindOrganizationUser", user.Id, organization.Id));
        }
    }

    private async Task<User?> GetUserFromManualLinkingDataAsync(string userIdentifier)
    {
        User? user = null;
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
                user = claimedUser;
            }
            else
            {
                throw new Exception(_i18nService.T("UserIdAndTokenMismatch"));
            }
        }

        return user;
    }

    /// <summary>
    /// Tries to get the organization by the provider which is org id for us as we use the scheme
    /// to identify organizations - not identity providers.
    /// </summary>
    /// <param name="provider">Org id string from SSO scheme property</param>
    /// <exception cref="Exception">Errors if the provider string is not a valid org id guid or if the org cannot be found by the id.</exception>
    private async Task<Organization> GetOrganizationByProviderAsync(string provider)
    {
        if (!Guid.TryParse(provider, out var organizationId))
        {
            // TODO: support non-org (server-wide) SSO in the future?
            throw new Exception(_i18nService.T("SSOProviderIsNotAnOrgId", provider));
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            throw new Exception(_i18nService.T("CouldNotFindOrganization", organizationId));
        }

        return organization;
    }

    /// <summary>
    /// Attempts to get an <see cref="OrganizationUser"/> for a given organization
    /// by first checking for an existing user relationship, and if none is found,
    /// by looking up an invited user via their email address.
    /// </summary>
    /// <param name="user">The existing user entity to be looked up in OrganizationUsers table.</param>
    /// <param name="organizationId">Organization id from the provider data.</param>
    /// <param name="email">Email to use as a fallback in case of an invited user not in the Org Users
    /// table yet.</param>
    private async Task<OrganizationUser?> GetOrganizationUserByUserAndOrgOrEmailAsync(
        User? user,
        Guid organizationId,
        string? email)
    {
        OrganizationUser? orgUser = null;

        // Try to find OrgUser via existing User Id.
        // This covers any OrganizationUser state after they have accepted an invite.
        if (user != null)
        {
            var orgUsersByUserId = await _organizationUserRepository.GetManyByUserAsync(user.Id);
            orgUser = orgUsersByUserId.SingleOrDefault(u => u.OrganizationId == organizationId);
        }

        // If no Org User found by Existing User Id - search all the organization's users via email.
        // This covers users who are Invited but haven't accepted their invite yet.
        if (email != null)
        {
            orgUser ??= await _organizationUserRepository.GetByOrganizationEmailAsync(organizationId, email);
        }

        return orgUser;
    }

    private void EnsureUserNotInInvited(
        OrganizationUserStatusType status,
        string organizationDisplayName)
    {
        if (status == OrganizationUserStatusType.Invited)
        {
            // Org User is invited – must accept via email first
            throw new Exception(
                _i18nService.T("AcceptInviteBeforeUsingSSO", organizationDisplayName));
        }
    }

    private void WhiteListedOrgUserStatus(
        OrganizationUserStatusType statusToCheckAgainst,
        OrganizationUserStatusType[] allowedStatuses,
        string organizationDisplayNameForLogging)
    {
        // if this status is one of the allowed ones, just return
        if (allowedStatuses.Contains(statusToCheckAgainst))
        {
            return;
        }

        // otherwise throw the appropriate exception
        switch (statusToCheckAgainst)
        {
            case OrganizationUserStatusType.Revoked:
                // Revoked users may not be (auto)‑provisioned
                throw new Exception(
                    _i18nService.T("OrganizationUserAccessRevoked", organizationDisplayNameForLogging));
            default:
                // anything else is “unknown”
                throw new Exception(
                    _i18nService.T("OrganizationUserUnknownStatus", organizationDisplayNameForLogging));
        }
    }

    private IActionResult InvalidJson(string errorMessageKey, Exception? ex = null)
    {
        Response.StatusCode = ex == null ? 400 : 500;
        return Json(new ErrorResponseModel(_i18nService.T(errorMessageKey))
        {
            ExceptionMessage = ex?.Message,
            ExceptionStackTrace = ex?.StackTrace,
            InnerExceptionMessage = ex?.InnerException?.Message,
        });
    }

    private string? TryGetEmailAddressFromClaims(IEnumerable<Claim> claims, IEnumerable<string> additionalClaimTypes)
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

#nullable disable
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
#nullable restore

    private async Task CreateSsoUserRecordAsync(string providerUserId, Guid userId, Guid orgId,
        OrganizationUser orgUser)
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

        var ssoUser = new SsoUser { ExternalId = providerUserId, UserId = userId, OrganizationId = orgId, };
        await _ssoUserRepository.CreateAsync(ssoUser);
    }

#nullable disable
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
                var provider = HttpContext.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
                var handler = await provider.GetHandlerAsync(HttpContext, idp);

                if (handler is IAuthenticationSignOutHandler)
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
#nullable restore

    /**
     * Tries to get a user's email from the claims and SSO configuration data or the provider user id if
     * the claims email extraction returns null.
     */
    private string? TryGetEmailAddress(
        IEnumerable<Claim> claims,
        SsoConfigurationData config,
        string providerUserId)
    {
        var email = TryGetEmailAddressFromClaims(claims, config.GetAdditionalEmailClaimTypes());

        // If email isn't populated from claims and providerUserId has @, assume it is the email.
        if (string.IsNullOrWhiteSpace(email) && providerUserId.Contains("@"))
        {
            email = providerUserId;
        }

        return email;
    }

    public bool IsNativeClient(DIM.AuthorizationRequest context)
    {
        return !context.RedirectUri.StartsWith("https", StringComparison.Ordinal)
               && !context.RedirectUri.StartsWith("http", StringComparison.Ordinal);
    }
}
