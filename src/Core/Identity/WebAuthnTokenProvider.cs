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
using Bit.Core.Utilities;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using PeterO.Cbor;
using System.Security.Cryptography;

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
            var existingCredentials = keys.Select(key => key.Item2.Descriptor);

            // Old u2f tokens
            var u2fProvider = user.GetTwoFactorProvider(TwoFactorProviderType.U2f);
            var u2fKeys = LoadU2fKeys(u2fProvider);
            var existingU2fCredentials = u2fKeys.Select(key => new PublicKeyCredentialDescriptor{
                Id = key.Item2.KeyHandleBytes,
                Type = PublicKeyCredentialType.PublicKey
            });

            var allCredentials = existingCredentials.Union(existingU2fCredentials).ToList();

            if (allCredentials.Count == 0)
            {
                return null;
            }

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
                AppID = CoreHelpers.U2fAppIdUrl(_globalSettings),
            };

            var options = _fido2.GetAssertionOptions(allCredentials, UserVerificationRequirement.Preferred, exts);

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

            var u2fProvider = user.GetTwoFactorProvider(TwoFactorProviderType.U2f);
            var u2fKeys = LoadU2fKeys(u2fProvider);

            if (keys.Count == 0 && u2fKeys.Count == 0)
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

            var webAuthCred = keys.Find(k => k.Item2.Descriptor.Id.SequenceEqual(clientResponse.Id));
            var u2fCred = u2fKeys.Find(k => k.Item2.KeyHandleBytes.SequenceEqual(clientResponse.Id));

            uint storedCounter;
            byte[] key;
            if (webAuthCred != null)
            {
                storedCounter = webAuthCred.Item2.SignatureCounter;
                key = webAuthCred.Item2.PublicKey;
            }
            else
            {
                storedCounter = u2fCred.Item2.Counter;
                key = CreatePublicKeyFromU2fRegistrationData(u2fCred.Item2.KeyHandleBytes, u2fCred.Item2.PublicKeyBytes).EncodeToBytes();
            }

            // 4. Create callback to check if userhandle owns the credentialId
            IsUserHandleOwnerOfCredentialIdAsync callback = async (args) =>
            {
                return true;
            };

            var test = new CredentialPublicKey(key);

            // 5. Make the assertion
            var res = await _fido2.MakeAssertionAsync(clientResponse, options, key, storedCounter, callback);

            return res.Status == "ok";
        }

        public static CBORObject CreatePublicKeyFromU2fRegistrationData(byte[] keyHandleData, byte[] publicKeyData)
        {
            var x = new byte[32];
            var y = new byte[32];
            Buffer.BlockCopy(publicKeyData, 1, x, 0, 32);
            Buffer.BlockCopy(publicKeyData, 33, y, 0, 32);

            var point = new ECPoint
            {
                X = x,
                Y = y,
            };

            var coseKey = CBORObject.NewMap();

            coseKey.Add(COSE.KeyCommonParameter.KeyType, COSE.KeyType.EC2);
            coseKey.Add(COSE.KeyCommonParameter.Alg, -7);

            coseKey.Add(COSE.KeyTypeParameter.Crv, COSE.EllipticCurve.P256);

            coseKey.Add(COSE.KeyTypeParameter.X, point.X);
            coseKey.Add(COSE.KeyTypeParameter.Y, point.Y);

            return coseKey;
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
