using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Bit.Core.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System;
using Bit.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Fido2NetLib.Objects;
using Fido2NetLib;
using Bit.Core.Utilities;
using Bit.Core.Settings;

namespace Bit.Core.Identity
{
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
            if (!(await userService.CanAccessPremium(user)))
            {
                return false;
            }

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
            if (!(await userService.CanAccessPremium(user)))
            {
                return null;
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn);
            var keys = LoadKeys(provider);
            var existingCredentials = keys.Select(key => key.Item2.Descriptor).ToList();

            if (existingCredentials.Count == 0)
            {
                return null;
            }

            var exts = new AuthenticationExtensionsClientInputs()
            {
                UserVerificationIndex = true,
                UserVerificationMethod = true,
                AppID = CoreHelpers.U2fAppIdUrl(_globalSettings),
            };

            var options = _fido2.GetAssertionOptions(existingCredentials, UserVerificationRequirement.Preferred, exts);

            provider.MetaData["login"] = options;

            var providers = user.GetTwoFactorProviders();
            providers[TwoFactorProviderType.WebAuthn] = provider;
            user.SetTwoFactorProviders(providers);
            await userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn);

            return options.ToJson();
        }

        public async Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
        {
            var userService = _serviceProvider.GetRequiredService<IUserService>();
            if (!(await userService.CanAccessPremium(user)) || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn);
            var keys = LoadKeys(provider);

            if (!provider.MetaData.ContainsKey("login"))
            {
                return false;
            }

            var clientResponse = JsonConvert.DeserializeObject<AuthenticatorAssertionRawResponse>(token);

            var jsonOptions = provider.MetaData["login"].ToString();
            var options = AssertionOptions.FromJson(jsonOptions);

            var webAuthCred = keys.Find(k => k.Item2.Descriptor.Id.SequenceEqual(clientResponse.Id));

            if (webAuthCred == null)
            {
                return false;
            }

            IsUserHandleOwnerOfCredentialIdAsync callback = (args) => Task.FromResult(true);

            var res = await _fido2.MakeAssertionAsync(clientResponse, options, webAuthCred.Item2.PublicKey, webAuthCred.Item2.SignatureCounter, callback);

            provider.MetaData.Remove("login");

            // Update SignatureCounter
            webAuthCred.Item2.SignatureCounter = res.Counter;

            var providers = user.GetTwoFactorProviders();
            providers[TwoFactorProviderType.WebAuthn].MetaData[webAuthCred.Item1] = webAuthCred.Item2;
            user.SetTwoFactorProviders(providers);
            await userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn);

            return res.Status == "ok";
        }

        private bool HasProperMetaData(TwoFactorProvider provider)
        {
            return (provider?.MetaData?.Count ?? 0) > 0;
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
}
