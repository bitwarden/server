using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Services;
using Bit.Core.Repositories;
using U2F.Core.Models;
using U2fLib = U2F.Core.Crypto.U2F;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using U2F.Core.Exceptions;

namespace Bit.Core.Identity
{
    public class U2fTokenProvider : IUserTwoFactorTokenProvider<User>
    {
        private readonly IU2fRepository _u2fRepository;
        private readonly IUserService _userService;
        private readonly GlobalSettings _globalSettings;

        public U2fTokenProvider(
            IU2fRepository u2fRepository,
            IUserService userService,
            GlobalSettings globalSettings)
        {
            _u2fRepository = u2fRepository;
            _userService = userService;
            _globalSettings = globalSettings;
        }

        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
        {
            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.U2f);
            var canGenerate = user.TwoFactorProviderIsEnabled(TwoFactorProviderType.U2f) && HasProperMetaData(provider);
            return Task.FromResult(canGenerate);
        }

        public async Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
        {
            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.U2f);
            if(!HasProperMetaData(provider))
            {
                return null;
            }

            var keys = new List<TwoFactorProvider.U2fMetaData>();

            var key1 = provider.MetaData["Key1"] as TwoFactorProvider.U2fMetaData;
            if(!key1?.Compromised ?? false)
            {
                keys.Add(key1);
            }

            if(keys.Count == 0)
            {
                return null;
            }

            await _u2fRepository.DeleteManyByUserIdAsync(user.Id);

            var challenges = new List<object>();
            foreach(var key in keys)
            {
                var registration = new DeviceRegistration(key.KeyHandleBytes, key.PublicKeyBytes,
                    key.CertificateBytes, key.Counter);
                var auth = U2fLib.StartAuthentication(_globalSettings.U2f.AppId, registration);

                // Maybe move this to a bulk create when we support more than 1 key?
                await _u2fRepository.CreateAsync(new U2f
                {
                    AppId = auth.AppId,
                    Challenge = auth.Challenge,
                    KeyHandle = auth.KeyHandle,
                    Version = auth.Version,
                    UserId = user.Id
                });

                challenges.Add(new
                {
                    appId = auth.AppId,
                    challenge = auth.Challenge,
                    keyHandle = auth.KeyHandle,
                    version = auth.Version
                });
            }

            var token = JsonConvert.SerializeObject(challenges);
            return token;
        }

        public async Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
        {
            if(string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
            if(!HasProperMetaData(provider))
            {
                return false;
            }

            var keys = new List<TwoFactorProvider.U2fMetaData>();

            var key1 = provider.MetaData["Key1"] as TwoFactorProvider.U2fMetaData;
            if(!key1?.Compromised ?? false)
            {
                keys.Add(key1);
            }

            if(keys.Count == 0)
            {
                return false;
            }

            var authenticateResponse = BaseModel.FromJson<AuthenticateResponse>(token);
            var key = keys.FirstOrDefault(f => f.KeyHandle == authenticateResponse.KeyHandle);

            if(key == null)
            {
                return false;
            }

            var challenges = await _u2fRepository.GetManyByUserIdAsync(user.Id);
            if(challenges.Count == 0)
            {
                return false;
            }

            var success = true;
            // User will have a authentication request for each device they have registered so get the one that matches
            // the device key handle
            var challenge = challenges.First(c => c.KeyHandle == authenticateResponse.KeyHandle);
            var registration = new DeviceRegistration(key.KeyHandleBytes, key.PublicKeyBytes, key.CertificateBytes,
                key.Counter);
            try
            {
                var auth = new StartedAuthentication(challenge.Challenge, challenge.AppId, challenge.KeyHandle);
                U2fLib.FinishAuthentication(auth, authenticateResponse, registration);
            }
            catch(U2fException)
            {
                success = false;
            }

            // Update database
            await _u2fRepository.DeleteManyByUserIdAsync(user.Id);
            key.Counter = registration.Counter;
            key.Compromised = registration.IsCompromised;

            var providers = user.GetTwoFactorProviders();
            providers[TwoFactorProviderType.U2f].MetaData["Key1"] = key;
            user.SetTwoFactorProviders(providers);
            await _userService.SaveUserAsync(user);

            return success;
        }

        private bool HasProperMetaData(TwoFactorProvider provider)
        {
            return provider?.MetaData != null && provider.MetaData.ContainsKey("Key1");
        }
    }
}
