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
using System;
using Bit.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Identity
{
    public class U2fTokenProvider : IUserTwoFactorTokenProvider<User>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IU2fRepository _u2fRepository;
        private readonly GlobalSettings _globalSettings;

        public U2fTokenProvider(
            IServiceProvider serviceProvider,
            IU2fRepository u2fRepository,
            GlobalSettings globalSettings)
        {
            _serviceProvider = serviceProvider;
            _u2fRepository = u2fRepository;
            _globalSettings = globalSettings;
        }

        public async Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
        {
            var userService = _serviceProvider.GetRequiredService<IUserService>();
            if(!(await userService.CanAccessPremium(user)))
            {
                return false;
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.U2f);
            if(!HasProperMetaData(provider))
            {
                return false;
            }

            return await user.TwoFactorProviderIsEnabledAsync(TwoFactorProviderType.U2f, userService);
        }

        public async Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
        {
            var userService = _serviceProvider.GetRequiredService<IUserService>();
            if(!(await userService.CanAccessPremium(user)))
            {
                return null;
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.U2f);
            if(!HasProperMetaData(provider))
            {
                return null;
            }

            var keys = new List<TwoFactorProvider.U2fMetaData>();

            var key1 = new TwoFactorProvider.U2fMetaData((dynamic)provider.MetaData["Key1"]);
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
                var auth = U2fLib.StartAuthentication(Utilities.CoreHelpers.U2fAppIdUrl(_globalSettings), registration);

                // Maybe move this to a bulk create when we support more than 1 key?
                await _u2fRepository.CreateAsync(new U2f
                {
                    AppId = auth.AppId,
                    Challenge = auth.Challenge,
                    KeyHandle = auth.KeyHandle,
                    Version = auth.Version,
                    UserId = user.Id,
                    CreationDate = DateTime.UtcNow
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
            var userService = _serviceProvider.GetRequiredService<IUserService>();
            if(!(await userService.CanAccessPremium(user)) || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.U2f);
            if(!HasProperMetaData(provider))
            {
                return false;
            }

            var keys = new List<TwoFactorProvider.U2fMetaData>();

            var key1 = new TwoFactorProvider.U2fMetaData((dynamic)provider.MetaData["Key1"]);
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

            // User will have a authentication request for each device they have registered so get the one that matches
            // the device key handle
            var challenge = challenges.FirstOrDefault(c => c.KeyHandle == authenticateResponse.KeyHandle);
            if(challenge == null)
            {
                return false;
            }

            var success = true;
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
            await manager.UpdateAsync(user);

            return success;
        }

        private bool HasProperMetaData(TwoFactorProvider provider)
        {
            return provider?.MetaData != null && provider.MetaData.ContainsKey("Key1");
        }
    }
}
