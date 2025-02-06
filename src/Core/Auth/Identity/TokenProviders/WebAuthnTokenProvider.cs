using System.Text.Json;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.Identity.TokenProviders;

public class WebAuthnTokenProvider : IUserTwoFactorTokenProvider<User>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IFido2 _fido2;
    private readonly GlobalSettings _globalSettings;

    public WebAuthnTokenProvider(IServiceProvider serviceProvider, IFido2 fido2, GlobalSettings globalSettings)
    {
        _serviceProvider = serviceProvider;
        _fido2 = fido2;
        _globalSettings = globalSettings;
    }

    public async Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
    {
        var userService = _serviceProvider.GetRequiredService<IUserService>();

        var webAuthnProvider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn);
        if (!HasProperMetaData(webAuthnProvider))
        {
            return false;
        }

        return await userService.TwoFactorProviderIsEnabledAsync(TwoFactorProviderType.WebAuthn, user);
    }

    public async Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
    {
        var userService = _serviceProvider.GetRequiredService<IUserService>();

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn);
        var keys = LoadKeys(provider);
        var existingCredentials = keys.Select(key => key.Item2.Descriptor).ToList();

        if (existingCredentials.Count == 0)
        {
            return null;
        }

        var exts = new AuthenticationExtensionsClientInputs()
        {
            UserVerificationMethod = true,
            AppID = CoreHelpers.U2fAppIdUrl(_globalSettings),
        };

        var options = _fido2.GetAssertionOptions(existingCredentials, UserVerificationRequirement.Discouraged, exts);

        // TODO: Remove this when newtonsoft legacy converters are gone
        provider.MetaData["login"] = JsonSerializer.Serialize(options);

        var providers = user.GetTwoFactorProviders();
        providers[TwoFactorProviderType.WebAuthn] = provider;
        user.SetTwoFactorProviders(providers);
        await userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn, logEvent: false);

        return options.ToJson();
    }

    public async Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
    {
        var userService = _serviceProvider.GetRequiredService<IUserService>();
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn);
        var keys = LoadKeys(provider);

        if (!provider.MetaData.ContainsKey("login"))
        {
            return false;
        }

        var clientResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(token,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var jsonOptions = provider.MetaData["login"].ToString();
        var options = AssertionOptions.FromJson(jsonOptions);

        var webAuthCred = keys.Find(k => k.Item2.Descriptor.Id.SequenceEqual(clientResponse.Id));

        if (webAuthCred == null)
        {
            return false;
        }

        // Callback to check user ownership of credential. Always return true since we have already
        // established ownership in this context.
        IsUserHandleOwnerOfCredentialIdAsync callback = (args, cancellationToken) => Task.FromResult(true);

        try
        {
            var res = await _fido2.MakeAssertionAsync(clientResponse, options, webAuthCred.Item2.PublicKey, webAuthCred.Item2.SignatureCounter, callback);

            provider.MetaData.Remove("login");

            // Update SignatureCounter
            webAuthCred.Item2.SignatureCounter = res.Counter;

            var providers = user.GetTwoFactorProviders();
            providers[TwoFactorProviderType.WebAuthn].MetaData[webAuthCred.Item1] = webAuthCred.Item2;
            user.SetTwoFactorProviders(providers);
            await userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn, logEvent: false);

            return res.Status == "ok";
        }
        catch (Fido2VerificationException)
        {
            return false;
        }

    }

    private bool HasProperMetaData(TwoFactorProvider provider)
    {
        return provider?.MetaData?.Any() ?? false;
    }

    private List<Tuple<string, TwoFactorProvider.WebAuthnData>> LoadKeys(TwoFactorProvider provider)
    {
        var keys = new List<Tuple<string, TwoFactorProvider.WebAuthnData>>();
        if (!HasProperMetaData(provider))
        {
            return keys;
        }

        // Support up to 5 keys
        for (var i = 1; i <= 5; i++)
        {
            var keyName = $"Key{i}";
            if (provider.MetaData.ContainsKey(keyName))
            {
                var key = new TwoFactorProvider.WebAuthnData((dynamic)provider.MetaData[keyName]);

                keys.Add(new Tuple<string, TwoFactorProvider.WebAuthnData>(keyName, key));
            }
        }

        return keys;
    }
}
