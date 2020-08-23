using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Repositories;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using U2fLib = U2F.Core.Crypto.U2F;
using U2F.Core.Models;
using U2F.Core.Exceptions;
using U2F.Core.Utils;
using System;
using Bit.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Fido2NetLib.Objects;
using Fido2NetLib;

namespace Bit.Core.Identity
{
    public class WebAuthnTokenProvider : IUserTwoFactorTokenProvider<User>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IU2fRepository _u2fRepository;
        private readonly IFido2 _fido2;
        private readonly IUserService _userService;
        private readonly GlobalSettings _globalSettings;

        public WebAuthnTokenProvider(
            IServiceProvider serviceProvider,
            IU2fRepository u2fRepository,
            IFido2 fido2,
            GlobalSettings globalSettings)
        {
            _serviceProvider = serviceProvider;
            _u2fRepository = u2fRepository;
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

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn);
            if (!HasProperMetaData(provider))
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
            if (keys.Count == 0)
            {
                return null;
            }

            var existingCredentials = LoadKeys(provider).Select(key => key.Item2.Descriptor).ToList();

            var exts = new AuthenticationExtensionsClientInputs()
            {
                SimpleTransactionAuthorization = "FIDO",
                GenericTransactionAuthorization = new TxAuthGenericArg
                {
                    ContentType = "text/plain",
                    Content = new byte[] { 0x46, 0x49, 0x44, 0x4F }
                },
                UserVerificationIndex = true,
                UserVerificationMethod = true,
            };

            var options = _fido2.GetAssertionOptions(existingCredentials, UserVerificationRequirement.Preferred, exts);

            provider.MetaData.Remove("login");
            provider.MetaData.Add("login", options);

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
            if (keys.Count == 0)
            {
                return false;
            }

            if (!provider.MetaData.ContainsKey("login"))
            {
                return false;
            }

            var clientResponse = JsonConvert.DeserializeObject<AuthenticatorAssertionRawResponse>(token);

            var jsonOptions = provider.MetaData["login"].ToString();
            var options = AssertionOptions.FromJson(jsonOptions);

            var creds = keys.Find(k => k.Item2.Descriptor.Id.SequenceEqual(clientResponse.Id)).Item2;

            // 3. Get credential counter from database
            var storedCounter = creds.SignatureCounter;

            // 4. Create callback to check if userhandle owns the credentialId
            IsUserHandleOwnerOfCredentialIdAsync callback = async (args) =>
            {
                return true;
            };

            // 5. Make the assertion
            var res = await _fido2.MakeAssertionAsync(clientResponse, options, creds.PublicKey, storedCounter, callback);

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
