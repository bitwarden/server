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
using PeterO.Cbor;
using System.Security.Cryptography;

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
            var u2fProvider = user.GetTwoFactorProvider(TwoFactorProviderType.U2f);
            if (!HasProperMetaData(webAuthnProvider) && !HasProperMetaData(u2fProvider))
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
            var existingCredentials = keys.Select(key => key.Item2.Descriptor);

            // Old u2f tokens
            var existingU2fCredentials = LoadU2fKeys(user.GetTwoFactorProvider(TwoFactorProviderType.U2f))
                .Select(key => new PublicKeyCredentialDescriptor
                {
                    Id = key.Item2.KeyHandleBytes,
                    Type = PublicKeyCredentialType.PublicKey
                });

            var allowedCredentials = existingCredentials.Union(existingU2fCredentials).ToList();
            if (allowedCredentials.Count == 0)
            {
                return null;
            }

            var exts = new AuthenticationExtensionsClientInputs()
            {
                UserVerificationIndex = true,
                UserVerificationMethod = true,
                AppID = CoreHelpers.U2fAppIdUrl(_globalSettings),
            };

            var options = _fido2.GetAssertionOptions(allowedCredentials, UserVerificationRequirement.Preferred, exts);

            provider.MetaData["login"] = options;

            var providers = user.GetTwoFactorProviders();
            providers.Remove(TwoFactorProviderType.WebAuthn);
            providers.Add(TwoFactorProviderType.WebAuthn, provider);
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

            var u2fProvider = user.GetTwoFactorProvider(TwoFactorProviderType.U2f);
            var u2fKeys = LoadU2fKeys(u2fProvider);

            if (!provider.MetaData.ContainsKey("login"))
            {
                return false;
            }

            var clientResponse = JsonConvert.DeserializeObject<AuthenticatorAssertionRawResponse>(token);

            var jsonOptions = provider.MetaData["login"].ToString();
            var options = AssertionOptions.FromJson(jsonOptions);

            var webAuthCred = keys.Find(k => k.Item2.Descriptor.Id.SequenceEqual(clientResponse.Id));
            var u2fCred = u2fKeys.Find(k => k.Item2.KeyHandleBytes.SequenceEqual(clientResponse.Id));

            if (webAuthCred == null && u2fCred == null)
            {
                return false;
            }

            var key = webAuthCred != null ? webAuthCred.Item2 : (TwoFactorProvider.BaseMetaData)u2fCred.Item2;

            IsUserHandleOwnerOfCredentialIdAsync callback = async (args) =>
            {
                return true;
            };

            var res = await _fido2.MakeAssertionAsync(clientResponse, options, key.GetPublicKey(), key.GetSignatureCounter(), callback);

            provider.MetaData.Remove("login");

            // Update SignatureCounter
            var providers = user.GetTwoFactorProviders();

            if (webAuthCred != null)
            {
                webAuthCred.Item2.SignatureCounter = res.Counter;
                providers[TwoFactorProviderType.WebAuthn].MetaData[webAuthCred.Item1] = webAuthCred.Item2;
            }
            providers.Remove(TwoFactorProviderType.WebAuthn);
            providers.Add(TwoFactorProviderType.WebAuthn, provider);

            if (u2fCred != null)
            {
                u2fCred.Item2.Counter = res.Counter;

                providers[TwoFactorProviderType.U2f].MetaData[u2fCred.Item1] = u2fCred.Item2;
                providers.Remove(TwoFactorProviderType.U2f);
                providers.Add(TwoFactorProviderType.U2f, u2fProvider);
                user.SetTwoFactorProviders(providers);
                await userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn);
            }

            user.SetTwoFactorProviders(providers);
            await userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn);
            if (u2fCred != null)
            {
                await userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.U2f);
            }

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

        private List<Tuple<string, TwoFactorProvider.U2fMetaData>> LoadU2fKeys(TwoFactorProvider provider)
        {
            var keys = new List<Tuple<string, TwoFactorProvider.U2fMetaData>>();
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
                    var key = new TwoFactorProvider.U2fMetaData((dynamic)provider.MetaData[keyName]);
                    if (!key?.Compromised ?? false)
                    {
                        keys.Add(new Tuple<string, TwoFactorProvider.U2fMetaData>(keyName, key));
                    }
                }
            }

            return keys;
        }
    }
}
