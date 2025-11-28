// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;

namespace Bit.Identity.IdentityServer.RequestValidators;

public class TwoFactorAuthenticationValidator(
    IUserService userService,
    UserManager<User> userManager,
    IOrganizationDuoUniversalTokenProvider organizationDuoWebTokenProvider,
    IApplicationCacheService applicationCacheService,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationRepository organizationRepository,
    IDataProtectorTokenFactory<SsoEmail2faSessionTokenable> ssoEmail2faSessionTokeFactory,
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
    ICurrentContext currentContext) : ITwoFactorAuthenticationValidator
{
    private readonly IUserService _userService = userService;
    private readonly UserManager<User> _userManager = userManager;
    private readonly IOrganizationDuoUniversalTokenProvider _organizationDuoUniversalTokenProvider = organizationDuoWebTokenProvider;
    private readonly IApplicationCacheService _applicationCacheService = applicationCacheService;
    private readonly IOrganizationUserRepository _organizationUserRepository = organizationUserRepository;
    private readonly IOrganizationRepository _organizationRepository = organizationRepository;
    private readonly IDataProtectorTokenFactory<SsoEmail2faSessionTokenable> _ssoEmail2faSessionTokeFactory = ssoEmail2faSessionTokeFactory;
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
    private readonly ICurrentContext _currentContext = currentContext;

    public async Task<Tuple<bool, Organization>> RequiresTwoFactorAsync(User user, ValidatedTokenRequest request)
    {
        if (request.GrantType == "client_credentials" || request.GrantType == "webauthn")
        {
            /*
                Do not require MFA for api key logins.
                We consider Fido2 userVerification a second factor, so we don't require a second factor here.
            */
            return new Tuple<bool, Organization>(false, null);
        }

        var individualRequired = _userManager.SupportsUserTwoFactor &&
                                 await _userManager.GetTwoFactorEnabledAsync(user) &&
                                 (await _userManager.GetValidTwoFactorProvidersAsync(user)).Count > 0;

        Organization firstEnabledOrg = null;
        var orgs = (await _currentContext.OrganizationMembershipAsync(_organizationUserRepository, user.Id)).ToList();
        if (orgs.Count > 0)
        {
            var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
            var twoFactorOrgs = orgs.Where(o => OrgUsing2fa(orgAbilities, o.Id));
            if (twoFactorOrgs.Any())
            {
                var userOrgs = await _organizationRepository.GetManyByUserIdAsync(user.Id);
                firstEnabledOrg = userOrgs.FirstOrDefault(
                    o => orgs.Any(om => om.Id == o.Id) && o.TwoFactorIsEnabled());
            }
        }

        return new Tuple<bool, Organization>(individualRequired || firstEnabledOrg != null, firstEnabledOrg);
    }

    public async Task<Dictionary<string, object>> BuildTwoFactorResultAsync(User user, Organization organization)
    {
        var enabledProviders = await GetEnabledTwoFactorProvidersAsync(user, organization);
        if (enabledProviders.Count == 0)
        {
            return null;
        }

        var providers = new Dictionary<string, Dictionary<string, object>>();
        foreach (var provider in enabledProviders)
        {
            var twoFactorParams = await BuildTwoFactorParams(organization, user, provider.Key, provider.Value);
            providers.Add(((byte)provider.Key).ToString(), twoFactorParams);
        }

        var twoFactorResultDict = new Dictionary<string, object>
        {
            { "TwoFactorProviders", providers.Keys }, // backwards compatibility
            { "TwoFactorProviders2", providers },
        };

        // If we have an Email 2FA provider we need this session token so SSO users
        // can re-request an email TOTP. The TwoFactorController.SendEmailLoginAsync
        // endpoint requires a way to authenticate the user before sending another email with
        // a TOTP, this token acts as the authentication mechanism.
        if (enabledProviders.Any(p => p.Key == TwoFactorProviderType.Email))
        {
            twoFactorResultDict.Add("SsoEmail2faSessionToken",
                _ssoEmail2faSessionTokeFactory.Protect(new SsoEmail2faSessionTokenable(user)));

            twoFactorResultDict.Add("Email", user.Email);
        }

        return twoFactorResultDict;
    }

    public async Task<bool> VerifyTwoFactorAsync(
        User user,
        Organization organization,
        TwoFactorProviderType type,
        string token)
    {
        if (organization != null && type == TwoFactorProviderType.OrganizationDuo)
        {
            if (organization.TwoFactorProviderIsEnabled(type))
            {
                return await _organizationDuoUniversalTokenProvider.ValidateAsync(token, organization, user);
            }
            return false;
        }

        if (type is TwoFactorProviderType.RecoveryCode)
        {
            return await _userService.RecoverTwoFactorAsync(user, token);
        }

        // These cases we want to always return false, U2f is deprecated and OrganizationDuo
        // uses a different flow than the other two factor providers, it follows the same
        // structure of a UserTokenProvider but has it's logic runs outside the usual token
        // provider flow. See IOrganizationDuoUniversalTokenProvider.cs
        if (type is TwoFactorProviderType.U2f or TwoFactorProviderType.OrganizationDuo)
        {
            return false;
        }

        // Now we are concerning the rest of the Two Factor Provider Types

        // The intent of this check is to make sure that the user is using a 2FA provider that
        // is enabled and allowed by their premium status.
        // The exception for Remember is because it is a "special" 2FA type that isn't ever explicitly
        // enabled by a user, so we can't check the user's 2FA providers to see if they're
        // enabled. We just have to check if the token is valid.
        if (type != TwoFactorProviderType.Remember &&
            user.GetTwoFactorProvider(type) == null)
        {
            return false;
        }

        // Finally, verify the token based on the provider type.
        return await _userManager.VerifyTwoFactorTokenAsync(
            user, CoreHelpers.CustomProviderName(type), token);
    }

    private async Task<List<KeyValuePair<TwoFactorProviderType, TwoFactorProvider>>> GetEnabledTwoFactorProvidersAsync(
        User user, Organization organization)
    {
        var enabledProviders = new List<KeyValuePair<TwoFactorProviderType, TwoFactorProvider>>();
        var organizationTwoFactorProviders = organization?.GetTwoFactorProviders();
        if (organizationTwoFactorProviders != null)
        {
            enabledProviders.AddRange(
                organizationTwoFactorProviders.Where(
                    p => (p.Value?.Enabled ?? false) && organization.Use2fa));
        }

        var userTwoFactorProviders = user.GetTwoFactorProviders();
        var userCanAccessPremium = await _userService.CanAccessPremium(user);
        if (userTwoFactorProviders != null)
        {
            enabledProviders.AddRange(
                userTwoFactorProviders.Where(p =>
                        // Providers that do not require premium
                        (p.Value.Enabled && !TwoFactorProvider.RequiresPremium(p.Key)) ||
                        // Providers that require premium and the User has Premium
                        (p.Value.Enabled && TwoFactorProvider.RequiresPremium(p.Key) && userCanAccessPremium)));
        }

        return enabledProviders;
    }

    /// <summary>
    /// Builds the parameters for the two-factor authentication
    /// </summary>
    /// <param name="organization">We need the organization for Organization Duo Provider type</param>
    /// <param name="user">The user for which the token is being generated</param>
    /// <param name="type">Provider Type</param>
    /// <param name="provider">Raw data that is used to create the response</param>
    /// <returns>a dictionary with the correct provider configuration or null if the provider is not configured properly</returns>
    private async Task<Dictionary<string, object>> BuildTwoFactorParams(Organization organization, User user,
        TwoFactorProviderType type, TwoFactorProvider provider)
    {
        // We will always return this dictionary. If none of the criteria is met then it will return null.
        var twoFactorParams = new Dictionary<string, object>();

        // OrganizationDuo is odd since it doesn't use the UserManager built-in TwoFactor flows
        /*
            Note: Duo is in the midst of being updated to use the UserManager built-in TwoFactor class
            in the future the `AuthUrl` will be the generated "token" - PM-8107
        */
        if (type == TwoFactorProviderType.OrganizationDuo &&
            await _organizationDuoUniversalTokenProvider.CanGenerateTwoFactorTokenAsync(organization))
        {
            twoFactorParams.Add("Host", provider.MetaData["Host"]);
            twoFactorParams.Add("AuthUrl",
                await _organizationDuoUniversalTokenProvider.GenerateAsync(organization, user));

            return twoFactorParams;
        }

        // Individual 2FA providers use the UserManager built-in TwoFactor flow so we can generate the token before building the params
        var token = await _userManager.GenerateTwoFactorTokenAsync(user,
            CoreHelpers.CustomProviderName(type));
        switch (type)
        {
            case TwoFactorProviderType.Duo:
                twoFactorParams.Add("Host", provider.MetaData["Host"]);
                twoFactorParams.Add("AuthUrl", token);
                break;
            case TwoFactorProviderType.WebAuthn:
                if (token != null)
                {
                    twoFactorParams = JsonSerializer.Deserialize<Dictionary<string, object>>(token);
                }
                break;
            case TwoFactorProviderType.Email:
                var twoFactorEmail = (string)provider.MetaData["Email"];
                var redactedEmail = CoreHelpers.RedactEmailAddress(twoFactorEmail);
                twoFactorParams.Add("Email", redactedEmail);
                break;
            case TwoFactorProviderType.YubiKey:
                twoFactorParams.Add("Nfc", (bool)provider.MetaData["Nfc"]);
                break;
        }

        // return null if the dictionary is empty
        return twoFactorParams.Count > 0 ? twoFactorParams : null;
    }

    private bool OrgUsing2fa(IDictionary<Guid, OrganizationAbility> orgAbilities, Guid orgId)
    {
        return orgAbilities != null && orgAbilities.TryGetValue(orgId, out var orgAbility) &&
               orgAbility.Enabled && orgAbility.Using2fa;
    }
}
