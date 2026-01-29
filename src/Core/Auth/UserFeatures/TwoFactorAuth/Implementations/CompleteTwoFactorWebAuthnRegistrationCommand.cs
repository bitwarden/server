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

public class CompleteTwoFactorWebAuthnRegistrationCommand : ICompleteTwoFactorWebAuthnRegistrationCommand
{
    private readonly IFido2 _fido2;
    private readonly IGlobalSettings _globalSettings;
    private readonly IHasPremiumAccessQuery _hasPremiumAccessQuery;
    private readonly IUserService _userService;

    public CompleteTwoFactorWebAuthnRegistrationCommand(IFido2 fido2,
        IGlobalSettings globalSettings,
        IHasPremiumAccessQuery hasPremiumAccessQuery,
        IUserService userService)
    {
        _fido2 = fido2;
        _globalSettings = globalSettings;
        _hasPremiumAccessQuery = hasPremiumAccessQuery;
        _userService = userService;
    }

    public async Task<bool> CompleteTwoFactorWebAuthnRegistrationAsync(User user, int id, string name,
        AuthenticatorAttestationRawResponse attestationResponse)
    {
        var keyId = $"Key{id}";

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn);
        if (provider?.MetaData is null || !provider.MetaData.TryGetValue("pending", out var pendingValue))
        {
            return false;
        }

        // Persistence-time validation for comprehensive enforcement. There is also boundary validation for best-possible UX.
        var maximumAllowedCredentialCount = await _hasPremiumAccessQuery.HasPremiumAccessAsync(user.Id)
            ? _globalSettings.WebAuthn.PremiumMaximumAllowedCredentials
            : _globalSettings.WebAuthn.NonPremiumMaximumAllowedCredentials;
        // Count only saved credentials ("Key{id}") toward the limit.
        if (provider.MetaData.Count(k => k.Key.StartsWith("Key")) >=
            maximumAllowedCredentialCount)
        {
            throw new BadRequestException("Maximum allowed WebAuthn credential count exceeded.");
        }

        var options = CredentialCreateOptions.FromJson((string)pendingValue);

        // Callback to ensure credential ID is unique. Always return true since we don't care if another
        // account uses the same 2FA key.
        IsCredentialIdUniqueToUserAsyncDelegate callback = (args, cancellationToken) => Task.FromResult(true);

        var success = await _fido2.MakeNewCredentialAsync(attestationResponse, options, callback);
        if (success.Result == null)
        {
            throw new BadRequestException("WebAuthn credential creation failed.");
        }

        provider.MetaData.Remove("pending");
        provider.MetaData[keyId] = new TwoFactorProvider.WebAuthnData
        {
            Name = name,
            Descriptor = new PublicKeyCredentialDescriptor(success.Result.CredentialId),
            PublicKey = success.Result.PublicKey,
            UserHandle = success.Result.User.Id,
            SignatureCounter = success.Result.Counter,
            CredType = success.Result.CredType,
            RegDate = DateTime.Now,
            AaGuid = success.Result.Aaguid
        };

        var providers = user.GetTwoFactorProviders();
        if (providers == null)
        {
            throw new BadRequestException("No two-factor provider found.");
        }
        providers[TwoFactorProviderType.WebAuthn] = provider;
        user.SetTwoFactorProviders(providers);
        await _userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn);

        return true;
    }
}
