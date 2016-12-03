using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Bit.Core.Domains;
using Bit.Core.Repositories;
using OtpSharp;
using Base32;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Bit.Core.Enums;

namespace Bit.Core.Services
{
    public class UserService : UserManager<User>, IUserService, IDisposable
    {
        private readonly IUserRepository _userRepository;
        private readonly ICipherRepository _cipherRepository;
        private readonly IMailService _mailService;
        private readonly IdentityErrorDescriber _identityErrorDescriber;
        private readonly IdentityOptions _identityOptions;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IEnumerable<IPasswordValidator<User>> _passwordValidators;

        public UserService(
            IUserRepository userRepository,
            ICipherRepository cipherRepository,
            IMailService mailService,
            IUserStore<User> store,
            IOptions<IdentityOptions> optionsAccessor,
            IPasswordHasher<User> passwordHasher,
            IEnumerable<IUserValidator<User>> userValidators,
            IEnumerable<IPasswordValidator<User>> passwordValidators,
            ILookupNormalizer keyNormalizer,
            IdentityErrorDescriber errors,
            IServiceProvider services,
            ILogger<UserManager<User>> logger)
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
            _mailService = mailService;
            _identityOptions = optionsAccessor?.Value ?? new IdentityOptions();
            _identityErrorDescriber = errors;
            _passwordHasher = passwordHasher;
            _passwordValidators = passwordValidators;
        }

        public async Task<User> GetUserByIdAsync(Guid userId)
        {
            return await _userRepository.GetByIdAsync(userId);
        }

        public async Task SaveUserAsync(User user)
        {
            if(user.Id == default(Guid))
            {
                throw new ApplicationException("Use register method to create a new user.");
            }

            user.RevisionDate = DateTime.UtcNow;
            await _userRepository.ReplaceAsync(user);
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

        public async Task<IdentityResult> ChangeEmailAsync(User user, string masterPassword, string newEmail, string newMasterPassword, string token, IEnumerable<Cipher> ciphers)
        {
            var verifyPasswordResult = _passwordHasher.VerifyHashedPassword(user, user.MasterPassword, masterPassword);
            if(verifyPasswordResult == PasswordVerificationResult.Failed)
            {
                return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
            }

            if(!await base.VerifyUserTokenAsync(user, _identityOptions.Tokens.ChangeEmailTokenProvider, GetChangeEmailTokenPurpose(newEmail), token))
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

            user.Email = newEmail;
            user.EmailVerified = true;
            user.RevisionDate = DateTime.UtcNow;

            if(ciphers.Any())
            {
                await _cipherRepository.UpdateUserEmailPasswordAndCiphersAsync(user, ciphers);
            }
            else
            {
                await _userRepository.ReplaceAsync(user);
            }

            return IdentityResult.Success;
        }

        public override Task<IdentityResult> ChangePasswordAsync(User user, string masterPassword, string newMasterPassword)
        {
            throw new NotImplementedException();
        }

        public async Task<IdentityResult> ChangePasswordAsync(User user, string masterPassword, string newMasterPassword, IEnumerable<Cipher> ciphers)
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

                user.RevisionDate = DateTime.UtcNow;
                if(ciphers.Any())
                {
                    await _cipherRepository.UpdateUserEmailPasswordAndCiphersAsync(user, ciphers);
                }
                else
                {
                    await _userRepository.ReplaceAsync(user);
                }

                return IdentityResult.Success;
            }

            Logger.LogWarning("Change password failed for user {userId}.", user.Id);
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

        public async Task GetTwoFactorAsync(User user, Enums.TwoFactorProvider provider)
        {
            if(user.TwoFactorEnabled && user.TwoFactorProvider.HasValue && user.TwoFactorProvider.Value == provider)
            {
                switch(provider)
                {
                    case Enums.TwoFactorProvider.Authenticator:
                        if(!string.IsNullOrWhiteSpace(user.AuthenticatorKey))
                        {
                            return;
                        }
                        break;
                    default:
                        throw new ArgumentException(nameof(provider));
                }
            }

            user.TwoFactorProvider = provider;
            // Reset authenticator key.
            user.AuthenticatorKey = null;

            switch(provider)
            {
                case Enums.TwoFactorProvider.Authenticator:
                    var key = KeyGeneration.GenerateRandomKey(20);
                    user.AuthenticatorKey = Base32Encoder.Encode(key);
                    break;
                default:
                    throw new ArgumentException(nameof(provider));
            }

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

            user.TwoFactorProvider = TwoFactorProvider.Authenticator;
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
                Logger.LogWarning("User {userId} password validation failed: {errors}.", await GetUserIdAsync(user), string.Join(";", errors.Select(e => e.Code)));
                return IdentityResult.Failed(errors.ToArray());
            }

            return IdentityResult.Success;
        }
    }
}
