
using System.Text.Json;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;

namespace Bit.Identity.IdentityServer;

public interface ITwoFactorAuthenticationValidator
{
    Task<Tuple<bool, Organization>> RequiresTwoFactorAsync(User user, ValidatedTokenRequest request);
    Task<Dictionary<string, object>> BuildTwoFactorResultAsync(User user, Organization organization);
    Task<bool> VerifyTwoFactor(User user, Organization organization, TwoFactorProviderType type, string token);
}

public class TwoFactorAuthenticationValidator(
    IUserService userService,
    UserManager<User> userManager,
    IOrganizationDuoWebTokenProvider organizationDuoWebTokenProvider,
    ITemporaryDuoWebV4SDKService duoWebV4SDKService,
    IFeatureService featureService,
    IApplicationCacheService applicationCacheService,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationRepository organizationRepository,
    IDataProtectorTokenFactory<SsoEmail2faSessionTokenable> ssoEmail2faSessionTokeFactory,
    ICurrentContext currentContext) : ITwoFactorAuthenticationValidator
{
    private readonly IUserService _userService = userService;
    private readonly UserManager<User> _userManager = userManager;
    private readonly IOrganizationDuoWebTokenProvider _organizationDuoWebTokenProvider = organizationDuoWebTokenProvider;
    private readonly ITemporaryDuoWebV4SDKService _duoWebV4SDKService = duoWebV4SDKService;
    private readonly IFeatureService _featureService = featureService;
    private readonly IApplicationCacheService _applicationCacheService = applicationCacheService;
    private readonly IOrganizationUserRepository _organizationUserRepository = organizationUserRepository;
    private readonly IOrganizationRepository _organizationRepository = organizationRepository;
    private readonly IDataProtectorTokenFactory<SsoEmail2faSessionTokenable> _ssoEmail2faSessionTokeFactory = ssoEmail2faSessionTokeFactory;
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
        var providerKeys = new List<byte>();
        var providers = new Dictionary<string, Dictionary<string, object>>();

        var enabledProviders = new List<KeyValuePair<TwoFactorProviderType, TwoFactorProvider>>();
        if (organization?.GetTwoFactorProviders() != null)
        {
            enabledProviders.AddRange(organization.GetTwoFactorProviders().Where(
                p => organization.TwoFactorProviderIsEnabled(p.Key)));
        }

        if (user.GetTwoFactorProviders() != null)
        {
            foreach (var p in user.GetTwoFactorProviders())
            {
                if (await _userService.TwoFactorProviderIsEnabledAsync(p.Key, user))
                {
                    enabledProviders.Add(p);
                }
            }
        }

        if (!enabledProviders.Any())
        {
            return null;
            // await BuildErrorResultAsync("No two-step providers enabled.", false, context, user);
        }

        foreach (var provider in enabledProviders)
        {
            providerKeys.Add((byte)provider.Key);
            var twoFactorParams = await BuildTwoFactorParams(organization, user, provider.Key, provider.Value);
            providers.Add(((byte)provider.Key).ToString(), twoFactorParams);
        }

        var twoFactorResultDict = new Dictionary<string, object>
        {
            { "TwoFactorProviders", providers.Keys },
            { "TwoFactorProviders2", providers },
        };

        // If we have email as a 2FA provider, we might need an SsoEmail2fa Session Token
        if (enabledProviders.Any(p => p.Key == TwoFactorProviderType.Email))
        {
            twoFactorResultDict.Add("SsoEmail2faSessionToken",
                _ssoEmail2faSessionTokeFactory.Protect(new SsoEmail2faSessionTokenable(user)));

            twoFactorResultDict.Add("Email", user.Email);
        }

        if (enabledProviders.Count == 1 && enabledProviders.First().Key == TwoFactorProviderType.Email)
        {
            // Send email now if this is their only 2FA method
            await _userService.SendTwoFactorEmailAsync(user);
        }

        return twoFactorResultDict;
    }

    public async Task<bool> VerifyTwoFactor(
        User user,
        Organization organization,
        TwoFactorProviderType type,
        string token)
    {
        switch (type)
        {
            case TwoFactorProviderType.Authenticator:
            case TwoFactorProviderType.Email:
            case TwoFactorProviderType.Duo:
            case TwoFactorProviderType.YubiKey:
            case TwoFactorProviderType.WebAuthn:
            case TwoFactorProviderType.Remember:
                if (type != TwoFactorProviderType.Remember &&
                    !await _userService.TwoFactorProviderIsEnabledAsync(type, user))
                {
                    return false;
                }
                // DUO SDK v4 Update: try to validate the token - PM-5156 addresses tech debt
                if (_featureService.IsEnabled(FeatureFlagKeys.DuoRedirect))
                {
                    if (type == TwoFactorProviderType.Duo)
                    {
                        if (!token.Contains(':'))
                        {
                            // We have to send the provider to the DuoWebV4SDKService to create the DuoClient
                            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
                            return await _duoWebV4SDKService.ValidateAsync(token, provider, user);
                        }
                    }
                }

                return await _userManager.VerifyTwoFactorTokenAsync(user,
                    CoreHelpers.CustomProviderName(type), token);
            case TwoFactorProviderType.OrganizationDuo:
                if (!organization?.TwoFactorProviderIsEnabled(type) ?? true)
                {
                    return false;
                }

                // DUO SDK v4 Update: try to validate the token - PM-5156 addresses tech debt
                if (_featureService.IsEnabled(FeatureFlagKeys.DuoRedirect))
                {
                    if (type == TwoFactorProviderType.OrganizationDuo)
                    {
                        if (!token.Contains(':'))
                        {
                            // We have to send the provider to the DuoWebV4SDKService to create the DuoClient
                            var provider = organization.GetTwoFactorProvider(TwoFactorProviderType.OrganizationDuo);
                            return await _duoWebV4SDKService.ValidateAsync(token, provider, user);
                        }
                    }
                }

                return await _organizationDuoWebTokenProvider.ValidateAsync(token, organization, user);
            default:
                return false;
        }
    }

    private async Task<Dictionary<string, object>> BuildTwoFactorParams(Organization organization, User user,
        TwoFactorProviderType type, TwoFactorProvider provider)
    {
        switch (type)
        {
            case TwoFactorProviderType.Duo:
            case TwoFactorProviderType.WebAuthn:
            case TwoFactorProviderType.Email:
            case TwoFactorProviderType.YubiKey:
                if (!await _userService.TwoFactorProviderIsEnabledAsync(type, user))
                {
                    return null;
                }

                var token = await _userManager.GenerateTwoFactorTokenAsync(user,
                    CoreHelpers.CustomProviderName(type));
                if (type == TwoFactorProviderType.Duo)
                {
                    var duoResponse = new Dictionary<string, object>
                    {
                        ["Host"] = provider.MetaData["Host"],
                        ["AuthUrl"] = await _duoWebV4SDKService.GenerateAsync(provider, user),
                    };

                    return duoResponse;
                }
                else if (type == TwoFactorProviderType.WebAuthn)
                {
                    if (token == null)
                    {
                        return null;
                    }

                    return JsonSerializer.Deserialize<Dictionary<string, object>>(token);
                }
                else if (type == TwoFactorProviderType.Email)
                {
                    var twoFactorEmail = (string)provider.MetaData["Email"];
                    var redactedEmail = CoreHelpers.RedactEmailAddress(twoFactorEmail);
                    return new Dictionary<string, object> { ["Email"] = redactedEmail };
                }
                else if (type == TwoFactorProviderType.YubiKey)
                {
                    return new Dictionary<string, object> { ["Nfc"] = (bool)provider.MetaData["Nfc"] };
                }

                return null;
            case TwoFactorProviderType.OrganizationDuo:
                if (await _organizationDuoWebTokenProvider.CanGenerateTwoFactorTokenAsync(organization))
                {
                    var duoResponse = new Dictionary<string, object>
                    {
                        ["Host"] = provider.MetaData["Host"],
                        ["AuthUrl"] = await _duoWebV4SDKService.GenerateAsync(provider, user),
                    };

                    return duoResponse;
                }
                return null;
            default:
                return null;
        }
    }

    private bool OrgUsing2fa(IDictionary<Guid, OrganizationAbility> orgAbilities, Guid orgId)
    {
        return orgAbilities != null && orgAbilities.ContainsKey(orgId) &&
               orgAbilities[orgId].Enabled && orgAbilities[orgId].Using2fa;
    }
}
