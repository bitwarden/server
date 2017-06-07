using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Bit.Core.Enums;
using OtpNet;
using System.Security.Claims;
using Bit.Core.Models;

namespace Bit.Core.Services
{
    public class UserService : UserManager<User>, IUserService, IDisposable
    {
        private readonly IUserRepository _userRepository;
        private readonly ICipherRepository _cipherRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IMailService _mailService;
        private readonly IPushNotificationService _pushService;
        private readonly IdentityErrorDescriber _identityErrorDescriber;
        private readonly IdentityOptions _identityOptions;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IEnumerable<IPasswordValidator<User>> _passwordValidators;
        private readonly CurrentContext _currentContext;

        public UserService(
            IUserRepository userRepository,
            ICipherRepository cipherRepository,
            IOrganizationUserRepository organizationUserRepository,
            IMailService mailService,
            IPushNotificationService pushService,
            IUserStore<User> store,
            IOptions<IdentityOptions> optionsAccessor,
            IPasswordHasher<User> passwordHasher,
            IEnumerable<IUserValidator<User>> userValidators,
            IEnumerable<IPasswordValidator<User>> passwordValidators,
            ILookupNormalizer keyNormalizer,
            IdentityErrorDescriber errors,
            IServiceProvider services,
            ILogger<UserManager<User>> logger,
            CurrentContext currentContext)
            : base(
                  store,
                  optionsAccessor,
                  passwordHasher,
                  userValidators,
                  passwordValidators,
                  keyNormalizer,
                  errors,
                  services,
                  logger)
        {
            _userRepository = userRepository;
            _cipherRepository = cipherRepository;
            _organizationUserRepository = organizationUserRepository;
            _mailService = mailService;
            _pushService = pushService;
            _identityOptions = optionsAccessor?.Value ?? new IdentityOptions();
            _identityErrorDescriber = errors;
            _passwordHasher = passwordHasher;
            _passwordValidators = passwordValidators;
            _currentContext = currentContext;
        }

        public Guid? GetProperUserId(ClaimsPrincipal principal)
        {
            Guid userIdGuid;
            if(!Guid.TryParse(GetUserId(principal), out userIdGuid))
            {
                return null;
            }

            return userIdGuid;
        }

        public async Task<User> GetUserByIdAsync(string userId)
        {
            if(_currentContext?.User != null &&
                string.Equals(_currentContext.User.Id.ToString(), userId, StringComparison.InvariantCultureIgnoreCase))
            {
                return _currentContext.User;
            }

            Guid userIdGuid;
            if(!Guid.TryParse(userId, out userIdGuid))
            {
                return null;
            }

            _currentContext.User = await _userRepository.GetByIdAsync(userIdGuid);
            return _currentContext.User;
        }

        public async Task<User> GetUserByIdAsync(Guid userId)
        {
            if(_currentContext?.User != null && _currentContext.User.Id == userId)
            {
                return _currentContext.User;
            }

            _currentContext.User = await _userRepository.GetByIdAsync(userId);
            return _currentContext.User;
        }

        public async Task<User> GetUserByPrincipalAsync(ClaimsPrincipal principal)
        {
            var userId = GetProperUserId(principal);
            if(!userId.HasValue)
            {
                return null;
            }

            return await GetUserByIdAsync(userId.Value);
        }

        public async Task<DateTime> GetAccountRevisionDateByIdAsync(Guid userId)
        {
            return await _userRepository.GetAccountRevisionDateAsync(userId);
        }

        public async Task SaveUserAsync(User user)
        {
            if(user.Id == default(Guid))
            {
                throw new ApplicationException("Use register method to create a new user.");
            }

            user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
            await _userRepository.ReplaceAsync(user);

            // push
            await _pushService.PushSyncSettingsAsync(user.Id);
        }

        public override async Task<IdentityResult> DeleteAsync(User user)
        {
            // Check if user is the owner of any organizations.
            var organizationOwnerCount = await _organizationUserRepository.GetCountByOrganizationOwnerUserAsync(user.Id);
            if(organizationOwnerCount > 0)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Description = "You must leave or delete any organizations that you are the owner of first."
                });
            }

            await _userRepository.DeleteAsync(user);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> RegisterUserAsync(User user, string masterPassword)
        {
            var result = await base.CreateAsync(user, masterPassword);
            if(result == IdentityResult.Success)
            {
                await _mailService.SendWelcomeEmailAsync(user);
            }

            return result;
        }

        public async Task SendMasterPasswordHintAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if(user == null)
            {
                // No user exists. Do we want to send an email telling them this in the future?
                return;
            }

            if(string.IsNullOrWhiteSpace(user.MasterPasswordHint))
            {
                await _mailService.SendNoMasterPasswordHintEmailAsync(email);
                return;
            }

            await _mailService.SendMasterPasswordHintEmailAsync(email, user.MasterPasswordHint);
        }

        public async Task InitiateEmailChangeAsync(User user, string newEmail)
        {
            var existingUser = await _userRepository.GetByEmailAsync(newEmail);
            if(existingUser != null)
            {
                await _mailService.SendChangeEmailAlreadyExistsEmailAsync(user.Email, newEmail);
                return;
            }

            var token = await base.GenerateChangeEmailTokenAsync(user, newEmail);
            await _mailService.SendChangeEmailEmailAsync(newEmail, token);
        }

        public async Task<IdentityResult> ChangeEmailAsync(User user, string masterPassword, string newEmail,
            string newMasterPassword, string token, string key)
        {
            var verifyPasswordResult = _passwordHasher.VerifyHashedPassword(user, user.MasterPassword, masterPassword);
            if(verifyPasswordResult == PasswordVerificationResult.Failed)
            {
                return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
            }

            if(!await base.VerifyUserTokenAsync(user, _identityOptions.Tokens.ChangeEmailTokenProvider,
                GetChangeEmailTokenPurpose(newEmail), token))
            {
                return IdentityResult.Failed(_identityErrorDescriber.InvalidToken());
            }

            var existingUser = await _userRepository.GetByEmailAsync(newEmail);
            if(existingUser != null && existingUser.Id != user.Id)
            {
                return IdentityResult.Failed(_identityErrorDescriber.DuplicateEmail(newEmail));
            }

            var result = await UpdatePasswordHash(user, newMasterPassword);
            if(!result.Succeeded)
            {
                return result;
            }

            user.Key = key;
            user.Email = newEmail;
            user.EmailVerified = true;
            user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
            await _userRepository.ReplaceAsync(user);

            return IdentityResult.Success;
        }

        public override Task<IdentityResult> ChangePasswordAsync(User user, string masterPassword, string newMasterPassword)
        {
            throw new NotImplementedException();
        }

        public async Task<IdentityResult> ChangePasswordAsync(User user, string masterPassword, string newMasterPassword,
            string key)
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if(await base.CheckPasswordAsync(user, masterPassword))
            {
                var result = await UpdatePasswordHash(user, newMasterPassword);
                if(!result.Succeeded)
                {
                    return result;
                }

                user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
                user.Key = key;
                await _userRepository.ReplaceAsync(user);

                return IdentityResult.Success;
            }

            Logger.LogWarning("Change password failed for user {userId}.", user.Id);
            return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
        }

        public async Task<IdentityResult> UpdateKeyAsync(User user, string masterPassword, string key, string privateKey,
            IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders)
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if(await base.CheckPasswordAsync(user, masterPassword))
            {
                user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
                user.SecurityStamp = Guid.NewGuid().ToString();
                user.Key = key;
                user.PrivateKey = privateKey;
                if(ciphers.Any() || folders.Any())
                {
                    await _cipherRepository.UpdateUserKeysAndCiphersAsync(user, ciphers, folders);
                }
                else
                {
                    await _userRepository.ReplaceAsync(user);
                }

                return IdentityResult.Success;
            }

            Logger.LogWarning("Update key for user {userId}.", user.Id);
            return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
        }

        public async Task<IdentityResult> RefreshSecurityStampAsync(User user, string masterPassword)
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if(await base.CheckPasswordAsync(user, masterPassword))
            {
                var result = await base.UpdateSecurityStampAsync(user);
                if(!result.Succeeded)
                {
                    return result;
                }

                await SaveUserAsync(user);
                return IdentityResult.Success;
            }

            Logger.LogWarning("Refresh security stamp failed for user {userId}.", user.Id);
            return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
        }

        public async Task SetupTwoFactorAsync(User user, TwoFactorProviderType provider)
        {
            var providers = user.GetTwoFactorProviders();
            if(providers != null && providers.ContainsKey(provider) && providers[provider].Enabled &&
                user.TwoFactorProvider.HasValue && user.TwoFactorProvider.Value == provider)
            {
                switch(provider)
                {
                    case TwoFactorProviderType.Authenticator:
                        if(!string.IsNullOrWhiteSpace(providers[provider].MetaData["Key"]))
                        {
                            return;
                        }
                        break;
                    default:
                        throw new ArgumentException(nameof(provider));
                }
            }

            if(providers == null)
            {
                providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
            }

            TwoFactorProvider providerInfo = null;
            if(!providers.ContainsKey(provider))
            {
                providerInfo = new TwoFactorProvider();
                providers.Add(provider, providerInfo);
            }
            else
            {
                providerInfo = providers[provider];
            }

            switch(provider)
            {
                case TwoFactorProviderType.Authenticator:
                    var key = KeyGeneration.GenerateRandomKey(20);
                    providerInfo.MetaData["Key"] = Base32Encoding.ToString(key);
                    providerInfo.Remember = true;
                    break;
                default:
                    throw new ArgumentException(nameof(provider));
            }

            user.TwoFactorProvider = provider;
            user.SetTwoFactorProviders(providers);
            await SaveUserAsync(user);
        }

        public async Task UpdateTwoFactorProviderAsync(User user, TwoFactorProviderType type)
        {
            var providers = user.GetTwoFactorProviders();
            if(!providers?.ContainsKey(type) ?? true)
            {
                return;
            }

            providers[type].Enabled = user.TwoFactorEnabled;
            user.SetTwoFactorProviders(providers);

            user.TwoFactorProvider = type;
            user.TwoFactorRecoveryCode = user.TwoFactorIsEnabled() ? Guid.NewGuid().ToString("N") : null;
            await SaveUserAsync(user);
        }

        public async Task<bool> RecoverTwoFactorAsync(string email, string masterPassword, string recoveryCode)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if(user == null)
            {
                // No user exists. Do we want to send an email telling them this in the future?
                return false;
            }

            if(!await base.CheckPasswordAsync(user, masterPassword))
            {
                return false;
            }

            if(string.Compare(user.TwoFactorRecoveryCode, recoveryCode, true) != 0)
            {
                return false;
            }

            user.TwoFactorEnabled = false;
            user.TwoFactorRecoveryCode = null;
            await SaveUserAsync(user);

            return true;
        }

        private async Task<IdentityResult> UpdatePasswordHash(User user, string newPassword, bool validatePassword = true)
        {
            if(validatePassword)
            {
                var validate = await ValidatePasswordInternal(user, newPassword);
                if(!validate.Succeeded)
                {
                    return validate;
                }
            }

            user.MasterPassword = _passwordHasher.HashPassword(user, newPassword);
            user.SecurityStamp = Guid.NewGuid().ToString();

            return IdentityResult.Success;
        }

        private async Task<IdentityResult> ValidatePasswordInternal(User user, string password)
        {
            var errors = new List<IdentityError>();
            foreach(var v in _passwordValidators)
            {
                var result = await v.ValidateAsync(this, user, password);
                if(!result.Succeeded)
                {
                    errors.AddRange(result.Errors);
                }
            }

            if(errors.Count > 0)
            {
                Logger.LogWarning("User {userId} password validation failed: {errors}.", await GetUserIdAsync(user),
                    string.Join(";", errors.Select(e => e.Code)));
                return IdentityResult.Failed(errors.ToArray());
            }

            return IdentityResult.Success;
        }
    }
}
