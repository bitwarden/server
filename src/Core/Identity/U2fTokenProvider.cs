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

            return await userService.TwoFactorProviderIsEnabledAsync(TwoFactorProviderType.U2f, user);
        }

        public async Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
        {
            var userService = _serviceProvider.GetRequiredService<IUserService>();
            if(!(await userService.CanAccessPremium(user)))
            {
                return null;
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.U2f);
            var keys = LoadKeys(provider);
            if(keys.Count == 0)
            {
                return null;
            }

            await _u2fRepository.DeleteManyByUserIdAsync(user.Id);

            try
            {
                var challengeBytes = U2fLib.Crypto.GenerateChallenge();
                var appId = Utilities.CoreHelpers.U2fAppIdUrl(_globalSettings);
                var oldChallenges = new List<object>();
                var challengeKeys = new List<object>();
                foreach(var key in keys)
                {
                    var registration = new DeviceRegistration(key.Item2.KeyHandleBytes, key.Item2.PublicKeyBytes,
                        key.Item2.CertificateBytes, key.Item2.Counter);
                    var auth = U2fLib.StartAuthentication(appId, registration, challengeBytes);

                    // TODO: Maybe move this to a bulk create?
                    await _u2fRepository.CreateAsync(new U2f
                    {
                        AppId = auth.AppId,
                        Challenge = auth.Challenge,
                        KeyHandle = auth.KeyHandle,
                        Version = auth.Version,
                        UserId = user.Id,
                        CreationDate = DateTime.UtcNow
                    });

                    challengeKeys.Add(new
                    {
                        keyHandle = auth.KeyHandle,
                        version = auth.Version
                    });

                    // TODO: Old challenges array is here for backwards compat. Remove in the future.
                    oldChallenges.Add(new
                    {
                        appId = auth.AppId,
                        challenge = auth.Challenge,
                        keyHandle = auth.KeyHandle,
                        version = auth.Version
                    });
                }

                var oldToken = JsonConvert.SerializeObject(oldChallenges);
                var token = JsonConvert.SerializeObject(new
                {
                    appId = appId,
                    challenge = challengeBytes.ByteArrayToBase64String(),
                    keys = challengeKeys
                });
                return $"{token}|{oldToken}";
            }
            catch(U2fException)
            {
                return null;
            }
        }

        public async Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
        {
            var userService = _serviceProvider.GetRequiredService<IUserService>();
            if(!(await userService.CanAccessPremium(user)) || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.U2f);
            var keys = LoadKeys(provider);
            if(keys.Count == 0)
            {
                return false;
            }

            var authenticateResponse = BaseModel.FromJson<AuthenticateResponse>(token);
            var key = keys.FirstOrDefault(f => f.Item2.KeyHandle == authenticateResponse.KeyHandle);

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
            var registration = new DeviceRegistration(key.Item2.KeyHandleBytes, key.Item2.PublicKeyBytes,
                key.Item2.CertificateBytes, key.Item2.Counter);
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
            key.Item2.Counter = registration.Counter;
            if(key.Item2.Counter > 0)
            {
                key.Item2.Compromised = registration.IsCompromised;
            }

            var providers = user.GetTwoFactorProviders();
            providers[TwoFactorProviderType.U2f].MetaData[key.Item1] = key.Item2;
            user.SetTwoFactorProviders(providers);
            await manager.UpdateAsync(user);

            return success;
        }

        private bool HasProperMetaData(TwoFactorProvider provider)
        {
            return (provider?.MetaData?.Count ?? 0) > 0;
        }

        private List<Tuple<string, TwoFactorProvider.U2fMetaData>> LoadKeys(TwoFactorProvider provider)
        {
            var keys = new List<Tuple<string, TwoFactorProvider.U2fMetaData>>();
            if(!HasProperMetaData(provider))
            {
                return keys;
            }

            // Support up to 5 keys
            for(var i = 1; i <= 5; i++)
            {
                var keyName = $"Key{i}";
                if(provider.MetaData.ContainsKey(keyName))
                {
                    var key = new TwoFactorProvider.U2fMetaData((dynamic)provider.MetaData[keyName]);
                    if(!key?.Compromised ?? false)
                    {
                        keys.Add(new Tuple<string, TwoFactorProvider.U2fMetaData>(keyName, key));
                    }
                }
            }

            return keys;
        }
    }
}
