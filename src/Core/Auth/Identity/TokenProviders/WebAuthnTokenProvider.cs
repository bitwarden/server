// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json;
using System.Text.Json.Nodes;
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

    public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
    {
        var webAuthnProvider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn);
        // null check happens in this method
        if (!HasProperMetaData(webAuthnProvider))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(webAuthnProvider.Enabled);
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

        var appId = CoreHelpers.U2fAppIdUrl(_globalSettings);

        var exts = new AuthenticationExtensionsClientInputs()
        {
            UserVerificationMethod = true,
            AppID = appId,
        };

        var options = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = existingCredentials,
            UserVerification = UserVerificationRequirement.Discouraged,
            Extensions = exts
        });

        // TODO: Remove this when newtonsoft legacy converters are gone
        provider.MetaData["login"] = WithAppIdExtension(JsonSerializer.Serialize(options), appId);

        var providers = user.GetTwoFactorProviders();
        providers[TwoFactorProviderType.WebAuthn] = provider;
        user.SetTwoFactorProviders(providers);
        await userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn, logEvent: false);

        return WithAppIdExtension(options.ToJson(), appId);
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

        if (!provider.MetaData.TryGetValue("login", out var login))
        {
            return false;
        }

        var clientResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(token,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var jsonOptions = login.ToString();
        var options = AssertionOptions.FromJson(jsonOptions);

        // Always apply the server's own U2F AppID, also after a Fido2 version that serializes it again.
        // The AppID never comes from the stored challenge or the client, so challenges stored without one still validate.
        options.Extensions ??= new AuthenticationExtensionsClientInputs();
        options.Extensions.AppID = CoreHelpers.U2fAppIdUrl(_globalSettings);

        var webAuthCred = keys.Find(k => k.Item2.Descriptor.Id.SequenceEqual(clientResponse.RawId));

        if (webAuthCred == null)
        {
            return false;
        }

        // Callback to check user ownership of credential. Always return true since we have already
        // established ownership in this context.
        IsUserHandleOwnerOfCredentialIdAsync callback = (args, cancellationToken) => Task.FromResult(true);

        try
        {
            var res = await _fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = clientResponse,
                OriginalOptions = options,
                StoredPublicKey = webAuthCred.Item2.PublicKey,
                StoredSignatureCounter = webAuthCred.Item2.SignatureCounter,
                IsUserHandleOwnerOfCredentialIdCallback = callback
            });

            provider.MetaData.Remove("login");

            // Update SignatureCounter
            webAuthCred.Item2.SignatureCounter = res.SignCount;

            var providers = user.GetTwoFactorProviders();
            providers[TwoFactorProviderType.WebAuthn].MetaData[webAuthCred.Item1] = webAuthCred.Item2;
            user.SetTwoFactorProviders(providers);
            await userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn, logEvent: false);

            return true;
        }
        catch (Fido2VerificationException)
        {
            return false;
        }

    }

    /// <summary>
    /// Checks if the provider has proper metadata.
    /// This is used to determine if the provider has been properly configured.
    /// </summary>
    /// <param name="provider"></param>
    /// <returns>true if metadata is present; false if empty or null</returns>
    private bool HasProperMetaData(TwoFactorProvider provider)
    {
        return provider?.MetaData?.Any() ?? false;
    }

    // Fido2 4.x does not serialize the appid extension input. Fido2 5.x fixes this, and then this helper is no longer needed.
    private static string WithAppIdExtension(string optionsJson, string appId)
    {
        var root = JsonNode.Parse(optionsJson)!.AsObject();
        if (root["extensions"] is not JsonObject extensions)
        {
            extensions = new JsonObject();
            root["extensions"] = extensions;
        }

        extensions["appid"] = appId;
        return root.ToJsonString();
    }

    private List<Tuple<string, TwoFactorProvider.WebAuthnData>> LoadKeys(TwoFactorProvider provider)
    {
        var keys = new List<Tuple<string, TwoFactorProvider.WebAuthnData>>();
        if (!HasProperMetaData(provider))
        {
            return keys;
        }

        // Load all WebAuthn credentials stored in metadata. The number of allowed credentials
        // is controlled by credential registration.
        foreach (var kvp in provider.MetaData.Where(k => k.Key.StartsWith("Key")))
        {
            var key = new TwoFactorProvider.WebAuthnData((dynamic)kvp.Value);
            keys.Add(new Tuple<string, TwoFactorProvider.WebAuthnData>(kvp.Key, key));
        }

        return keys;
    }
}
