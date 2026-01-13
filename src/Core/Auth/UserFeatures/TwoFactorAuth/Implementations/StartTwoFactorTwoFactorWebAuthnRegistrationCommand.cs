using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Billing.Premium.Queries;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Settings;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth.Implementations;

internal class StartTwoFactorTwoFactorWebAuthnRegistrationCommand : IStartTwoFactorWebAuthnRegistrationCommand
{
    private readonly IFido2 _fido2;
    private readonly IGlobalSettings _globalSettings;
    private readonly IHasPremiumAccessQuery _hasPremiumAccessQuery;
    private readonly IUserService _userService;

    public StartTwoFactorTwoFactorWebAuthnRegistrationCommand(
        IFido2 fido2,
        IGlobalSettings globalSettings,
        IHasPremiumAccessQuery hasPremiumAccessQuery,
        IUserService userService)
    {
        _fido2 = fido2;
        _globalSettings = globalSettings;
        _hasPremiumAccessQuery = hasPremiumAccessQuery;
        _userService = userService;
    }

    public async Task<CredentialCreateOptions> StartTwoFactorWebAuthnRegistrationAsync(User user)
    {
        var providers = user.GetTwoFactorProviders();
        if (providers == null)
        {
            providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
        }

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn);
        if (provider == null)
        {
            provider = new TwoFactorProvider
            {
                Enabled = false
            };
        }
        if (provider.MetaData == null)
        {
            provider.MetaData = new Dictionary<string, object>();
        }

        // Boundary validation to provide a better UX. There is also second-level enforcement at persistence time.
        var userHasPremiumAccess = await _hasPremiumAccessQuery.HasPremiumAccessAsync(user.Id);
        var maximumAllowedCredentialCount = userHasPremiumAccess
            ? _globalSettings.WebAuthn.PremiumMaximumAllowedCredentials
            : _globalSettings.WebAuthn.NonPremiumMaximumAllowedCredentials;

        // Count only saved credentials ("Key{id}") toward the limit.
        if (provider.MetaData.Count(k => k.Key.StartsWith("Key")) >=
            maximumAllowedCredentialCount)
        {
            throw new BadRequestException("Maximum allowed WebAuthn credential count exceeded.");
        }

        var fidoUser = new Fido2User
        {
            DisplayName = user.Name,
            Name = user.Email,
            Id = user.Id.ToByteArray(),
        };

        var excludeCredentials = provider.MetaData
            .Where(k => k.Key.StartsWith("Key"))
            .Select(k => new TwoFactorProvider.WebAuthnData((dynamic)k.Value).Descriptor)
            .ToList();

        var authenticatorSelection = new AuthenticatorSelection
        {
            AuthenticatorAttachment = null,
            RequireResidentKey = false,
            UserVerification = UserVerificationRequirement.Discouraged
        };
        var options = _fido2.RequestNewCredential(fidoUser, excludeCredentials, authenticatorSelection, AttestationConveyancePreference.None);

        provider.MetaData["pending"] = options.ToJson();
        providers[TwoFactorProviderType.WebAuthn] = provider;
        user.SetTwoFactorProviders(providers);
        await _userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn, false);

        return options;
    }
}
