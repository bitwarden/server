using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using System.Linq;
using Bit.Core.Enums;
using System.Security.Claims;
using Bit.Core.Models;
using Bit.Core.Models.Business;
using U2fLib = U2F.Core.Crypto.U2F;
using U2F.Core.Models;
using U2F.Core.Utils;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using System.IO;
using Newtonsoft.Json;
using Microsoft.AspNetCore.DataProtection;
using U2F.Core.Exceptions;

namespace Bit.Core.Services
{
    public class UserService : UserManager<User>, IUserService, IDisposable
    {
        private const string PremiumPlanId = "premium-annually";
        private const string StoragePlanId = "storage-gb-annually";

        private readonly IUserRepository _userRepository;
        private readonly ICipherRepository _cipherRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IU2fRepository _u2fRepository;
        private readonly IMailService _mailService;
        private readonly IPushNotificationService _pushService;
        private readonly IdentityErrorDescriber _identityErrorDescriber;
        private readonly IdentityOptions _identityOptions;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IEnumerable<IPasswordValidator<User>> _passwordValidators;
        private readonly ILicensingService _licenseService;
        private readonly IEventService _eventService;
        private readonly IApplicationCacheService _applicationCacheService;
        private readonly IPaymentService _paymentService;
        private readonly IDataProtector _organizationServiceDataProtector;
        private readonly CurrentContext _currentContext;
        private readonly GlobalSettings _globalSettings;

        public UserService(
            IUserRepository userRepository,
            ICipherRepository cipherRepository,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationRepository organizationRepository,
            IU2fRepository u2fRepository,
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
            ILicensingService licenseService,
            IEventService eventService,
            IApplicationCacheService applicationCacheService,
            IDataProtectionProvider dataProtectionProvider,
            IPaymentService paymentService,
            CurrentContext currentContext,
            GlobalSettings globalSettings)
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
            _organizationRepository = organizationRepository;
            _u2fRepository = u2fRepository;
            _mailService = mailService;
            _pushService = pushService;
            _identityOptions = optionsAccessor?.Value ?? new IdentityOptions();
            _identityErrorDescriber = errors;
            _passwordHasher = passwordHasher;
            _passwordValidators = passwordValidators;
            _licenseService = licenseService;
            _eventService = eventService;
            _applicationCacheService = applicationCacheService;
            _paymentService = paymentService;
            _organizationServiceDataProtector = dataProtectionProvider.CreateProtector(
                "OrganizationServiceDataProtector");
            _currentContext = currentContext;
            _globalSettings = globalSettings;
        }

        public Guid? GetProperUserId(ClaimsPrincipal principal)
        {
            if(!Guid.TryParse(GetUserId(principal), out var userIdGuid))
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

            if(!Guid.TryParse(userId, out var userIdGuid))
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

        public async Task SaveUserAsync(User user, bool push = false)
        {
            if(user.Id == default(Guid))
            {
                throw new ApplicationException("Use register method to create a new user.");
            }

            user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
            await _userRepository.ReplaceAsync(user);

            if(push)
            {
                // push
                await _pushService.PushSyncSettingsAsync(user.Id);
            }
        }

        public override async Task<IdentityResult> DeleteAsync(User user)
        {
            // Check if user is the only owner of any organizations.
            var onlyOwnerCount = await _organizationUserRepository.GetCountByOnlyOwnerAsync(user.Id);
            if(onlyOwnerCount > 0)
            {
                var deletedOrg = false;
                var orgs = await _organizationUserRepository.GetManyDetailsByUserAsync(user.Id,
                    OrganizationUserStatusType.Confirmed);
                if(orgs.Count == 1)
                {
                    var org = await _organizationRepository.GetByIdAsync(orgs.First().OrganizationId);
                    if(org != null && (!org.Enabled || string.IsNullOrWhiteSpace(org.GatewaySubscriptionId)))
                    {
                        var orgCount = await _organizationUserRepository.GetCountByOrganizationIdAsync(org.Id);
                        if(orgCount <= 1)
                        {
                            await _organizationRepository.DeleteAsync(org);
                            deletedOrg = true;
                        }
                    }
                }

                if(!deletedOrg)
                {
                    return IdentityResult.Failed(new IdentityError
                    {
                        Description = "You must leave or delete any organizations that you are the only owner of first."
                    });
                }
            }

            if(!string.IsNullOrWhiteSpace(user.GatewaySubscriptionId))
            {
                try
                {
                    await CancelPremiumAsync(user);
                }
                catch(GatewayException) { }
            }

            await _userRepository.DeleteAsync(user);
            await _pushService.PushLogOutAsync(user.Id);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> DeleteAsync(User user, string token)
        {
            if(!(await VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, "DeleteAccount", token)))
            {
                return IdentityResult.Failed(ErrorDescriber.InvalidToken());
            }

            return await DeleteAsync(user);
        }

        public async Task SendDeleteConfirmationAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if(user == null)
            {
                // No user exists.
                return;
            }

            var token = await base.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, "DeleteAccount");
            await _mailService.SendVerifyDeleteEmailAsync(user.Email, user.Id, token);
        }

        public async Task<IdentityResult> RegisterUserAsync(User user, string masterPassword,
            string token, Guid? orgUserId)
        {
            var tokenValid = false;
            if(_globalSettings.DisableUserRegistration && !string.IsNullOrWhiteSpace(token) && orgUserId.HasValue)
            {
                tokenValid = CoreHelpers.UserInviteTokenIsValid(_organizationServiceDataProtector, token,
                    user.Email, orgUserId.Value);
            }

            if(_globalSettings.DisableUserRegistration && !tokenValid)
            {
                throw new BadRequestException("Open registration has been disabled by the system administrator.");
            }

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

        public async Task SendTwoFactorEmailAsync(User user)
        {
            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Email);
            if(provider == null || provider.MetaData == null || !provider.MetaData.ContainsKey("Email"))
            {
                throw new ArgumentNullException("No email.");
            }

            var email = ((string)provider.MetaData["Email"]).ToLowerInvariant();
            var token = await base.GenerateUserTokenAsync(user, TokenOptions.DefaultEmailProvider,
                "2faEmail:" + email);
            await _mailService.SendTwoFactorEmailAsync(email, token);
        }

        public async Task<bool> VerifyTwoFactorEmailAsync(User user, string token)
        {
            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Email);
            if(provider == null || provider.MetaData == null || !provider.MetaData.ContainsKey("Email"))
            {
                throw new ArgumentNullException("No email.");
            }

            var email = ((string)provider.MetaData["Email"]).ToLowerInvariant();
            return await base.VerifyUserTokenAsync(user, TokenOptions.DefaultEmailProvider,
                "2faEmail:" + email, token);
        }

        public async Task<U2fRegistration> StartU2fRegistrationAsync(User user)
        {
            await _u2fRepository.DeleteManyByUserIdAsync(user.Id);
            var reg = U2fLib.StartRegistration(CoreHelpers.U2fAppIdUrl(_globalSettings));
            await _u2fRepository.CreateAsync(new U2f
            {
                AppId = reg.AppId,
                Challenge = reg.Challenge,
                Version = reg.Version,
                UserId = user.Id,
                CreationDate = DateTime.UtcNow
            });

            return new U2fRegistration
            {
                AppId = reg.AppId,
                Challenge = reg.Challenge,
                Version = reg.Version
            };
        }

        public async Task<bool> CompleteU2fRegistrationAsync(User user, int id, string name, string deviceResponse)
        {
            if(string.IsNullOrWhiteSpace(deviceResponse))
            {
                return false;
            }

            var challenges = await _u2fRepository.GetManyByUserIdAsync(user.Id);
            if(!challenges?.Any() ?? true)
            {
                return false;
            }

            var registerResponse = BaseModel.FromJson<RegisterResponse>(deviceResponse);

            try
            {
                var challenge = challenges.OrderBy(i => i.Id).Last(i => i.KeyHandle == null);
                var startedReg = new StartedRegistration(challenge.Challenge, challenge.AppId);
                var reg = U2fLib.FinishRegistration(startedReg, registerResponse);

                await _u2fRepository.DeleteManyByUserIdAsync(user.Id);

                // Add device
                var providers = user.GetTwoFactorProviders();
                if(providers == null)
                {
                    providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
                }
                var provider = user.GetTwoFactorProvider(TwoFactorProviderType.U2f);
                if(provider == null)
                {
                    provider = new TwoFactorProvider();
                }
                if(provider.MetaData == null)
                {
                    provider.MetaData = new Dictionary<string, object>();
                }

                if(provider.MetaData.Count >= 5)
                {
                    // Can only register up to 5 keys
                    return false;
                }

                var keyId = $"Key{id}";
                if(provider.MetaData.ContainsKey(keyId))
                {
                    provider.MetaData.Remove(keyId);
                }

                provider.Enabled = true;
                provider.MetaData.Add(keyId, new TwoFactorProvider.U2fMetaData
                {
                    Name = name,
                    KeyHandle = reg.KeyHandle == null ? null : Utils.ByteArrayToBase64String(reg.KeyHandle),
                    PublicKey = reg.PublicKey == null ? null : Utils.ByteArrayToBase64String(reg.PublicKey),
                    Certificate = reg.AttestationCert == null ? null : Utils.ByteArrayToBase64String(reg.AttestationCert),
                    Compromised = false,
                    Counter = reg.Counter
                });

                if(providers.ContainsKey(TwoFactorProviderType.U2f))
                {
                    providers.Remove(TwoFactorProviderType.U2f);
                }

                providers.Add(TwoFactorProviderType.U2f, provider);
                user.SetTwoFactorProviders(providers);
                await UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.U2f);
                return true;
            }
            catch(U2fException)
            {
                return false;
            }
        }

        public async Task<bool> DeleteU2fKeyAsync(User user, int id)
        {
            var providers = user.GetTwoFactorProviders();
            if(providers == null)
            {
                return false;
            }

            var keyName = $"Key{id}";
            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.U2f);
            if(!provider?.MetaData?.ContainsKey(keyName) ?? true)
            {
                return false;
            }

            if(provider.MetaData.Count < 2)
            {
                return false;
            }

            provider.MetaData.Remove(keyName);
            providers[TwoFactorProviderType.U2f] = provider;
            user.SetTwoFactorProviders(providers);
            await UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.U2f);
            return true;
        }

        public async Task SendEmailVerificationAsync(User user)
        {
            if(user.EmailVerified)
            {
                throw new BadRequestException("Email already verified.");
            }

            var token = await base.GenerateEmailConfirmationTokenAsync(user);
            await _mailService.SendVerifyEmailEmailAsync(user.Email, user.Id, token);
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
            await _pushService.PushLogOutAsync(user.Id);

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

            if(await CheckPasswordAsync(user, masterPassword))
            {
                var result = await UpdatePasswordHash(user, newMasterPassword);
                if(!result.Succeeded)
                {
                    return result;
                }

                user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
                user.Key = key;

                await _userRepository.ReplaceAsync(user);
                await _eventService.LogUserEventAsync(user.Id, EventType.User_ChangedPassword);
                await _pushService.PushLogOutAsync(user.Id);

                return IdentityResult.Success;
            }

            Logger.LogWarning("Change password failed for user {userId}.", user.Id);
            return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
        }

        public async Task<IdentityResult> ChangeKdfAsync(User user, string masterPassword, string newMasterPassword,
            string key, KdfType kdf, int kdfIterations)
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if(await CheckPasswordAsync(user, masterPassword))
            {
                var result = await UpdatePasswordHash(user, newMasterPassword);
                if(!result.Succeeded)
                {
                    return result;
                }

                user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
                user.Key = key;
                user.Kdf = kdf;
                user.KdfIterations = kdfIterations;
                await _userRepository.ReplaceAsync(user);
                await _pushService.PushLogOutAsync(user.Id);
                return IdentityResult.Success;
            }

            Logger.LogWarning("Change KDF failed for user {userId}.", user.Id);
            return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
        }

        public async Task<IdentityResult> UpdateKeyAsync(User user, string masterPassword, string key, string privateKey,
            IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders)
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if(await CheckPasswordAsync(user, masterPassword))
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

                await _pushService.PushLogOutAsync(user.Id);
                return IdentityResult.Success;
            }

            Logger.LogWarning("Update key failed for user {userId}.", user.Id);
            return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
        }

        public async Task<IdentityResult> RefreshSecurityStampAsync(User user, string masterPassword)
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if(await CheckPasswordAsync(user, masterPassword))
            {
                var result = await base.UpdateSecurityStampAsync(user);
                if(!result.Succeeded)
                {
                    return result;
                }

                await SaveUserAsync(user);
                await _pushService.PushLogOutAsync(user.Id);
                return IdentityResult.Success;
            }

            Logger.LogWarning("Refresh security stamp failed for user {userId}.", user.Id);
            return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
        }

        public async Task UpdateTwoFactorProviderAsync(User user, TwoFactorProviderType type)
        {
            var providers = user.GetTwoFactorProviders();
            if(!providers?.ContainsKey(type) ?? true)
            {
                return;
            }

            providers[type].Enabled = true;
            user.SetTwoFactorProviders(providers);

            if(string.IsNullOrWhiteSpace(user.TwoFactorRecoveryCode))
            {
                user.TwoFactorRecoveryCode = CoreHelpers.SecureRandomString(32, upper: false, special: false);
            }
            await SaveUserAsync(user);
            await _eventService.LogUserEventAsync(user.Id, EventType.User_Updated2fa);
        }

        public async Task DisableTwoFactorProviderAsync(User user, TwoFactorProviderType type)
        {
            var providers = user.GetTwoFactorProviders();
            if(!providers?.ContainsKey(type) ?? true)
            {
                return;
            }

            providers.Remove(type);
            user.SetTwoFactorProviders(providers);
            await SaveUserAsync(user);
            await _eventService.LogUserEventAsync(user.Id, EventType.User_Disabled2fa);
        }

        public async Task<bool> RecoverTwoFactorAsync(string email, string masterPassword, string recoveryCode)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if(user == null)
            {
                // No user exists. Do we want to send an email telling them this in the future?
                return false;
            }

            if(!await CheckPasswordAsync(user, masterPassword))
            {
                return false;
            }

            if(string.Compare(user.TwoFactorRecoveryCode, recoveryCode, true) != 0)
            {
                return false;
            }

            user.TwoFactorProviders = null;
            user.TwoFactorRecoveryCode = CoreHelpers.SecureRandomString(32, upper: false, special: false);
            await SaveUserAsync(user);
            await _eventService.LogUserEventAsync(user.Id, EventType.User_Recovered2fa);

            return true;
        }

        public async Task SignUpPremiumAsync(User user, string paymentToken, PaymentMethodType paymentMethodType,
            short additionalStorageGb, UserLicense license)
        {
            if(user.Premium)
            {
                throw new BadRequestException("Already a premium user.");
            }

            if(additionalStorageGb < 0)
            {
                throw new BadRequestException("You can't subtract storage!");
            }

            IPaymentService paymentService = null;
            if(_globalSettings.SelfHosted)
            {
                if(license == null || !_licenseService.VerifyLicense(license))
                {
                    throw new BadRequestException("Invalid license.");
                }

                if(!license.CanUse(user))
                {
                    throw new BadRequestException("This license is not valid for this user.");
                }

                var dir = $"{_globalSettings.LicenseDirectory}/user";
                Directory.CreateDirectory(dir);
                File.WriteAllText($"{dir}/{user.Id}.json", JsonConvert.SerializeObject(license, Formatting.Indented));
            }
            else
            {
                await _paymentService.PurchasePremiumAsync(user, paymentMethodType, paymentToken, additionalStorageGb);
            }

            user.Premium = true;
            user.RevisionDate = DateTime.UtcNow;

            if(_globalSettings.SelfHosted)
            {
                user.MaxStorageGb = 10240; // 10 TB
                user.LicenseKey = license.LicenseKey;
                user.PremiumExpirationDate = license.Expires;
            }
            else
            {
                user.MaxStorageGb = (short)(1 + additionalStorageGb);
                user.LicenseKey = CoreHelpers.SecureRandomString(20);
            }

            try
            {
                await SaveUserAsync(user);
                await _pushService.PushSyncVaultAsync(user.Id);
            }
            catch when(!_globalSettings.SelfHosted)
            {
                await paymentService.CancelAndRecoverChargesAsync(user);
                throw;
            }
        }

        public async Task UpdateLicenseAsync(User user, UserLicense license)
        {
            if(!_globalSettings.SelfHosted)
            {
                throw new InvalidOperationException("Licenses require self hosting.");
            }

            if(license == null || !_licenseService.VerifyLicense(license))
            {
                throw new BadRequestException("Invalid license.");
            }

            if(!license.CanUse(user))
            {
                throw new BadRequestException("This license is not valid for this user.");
            }

            var dir = $"{_globalSettings.LicenseDirectory}/user";
            Directory.CreateDirectory(dir);
            File.WriteAllText($"{dir}/{user.Id}.json", JsonConvert.SerializeObject(license, Formatting.Indented));

            user.Premium = license.Premium;
            user.RevisionDate = DateTime.UtcNow;
            user.MaxStorageGb = _globalSettings.SelfHosted ? 10240 : license.MaxStorageGb; // 10 TB
            user.LicenseKey = license.LicenseKey;
            user.PremiumExpirationDate = license.Expires;
            await SaveUserAsync(user);
        }

        public async Task AdjustStorageAsync(User user, short storageAdjustmentGb)
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if(!user.Premium)
            {
                throw new BadRequestException("Not a premium user.");
            }

            await BillingHelpers.AdjustStorageAsync(_paymentService, user, storageAdjustmentGb, StoragePlanId);
            await SaveUserAsync(user);
        }

        public async Task ReplacePaymentMethodAsync(User user, string paymentToken, PaymentMethodType paymentMethodType)
        {
            if(paymentToken.StartsWith("btok_"))
            {
                throw new BadRequestException("Invalid token.");
            }

            var updated = await _paymentService.UpdatePaymentMethodAsync(user, paymentMethodType, paymentToken);
            if(updated)
            {
                await SaveUserAsync(user);
            }
        }

        public async Task CancelPremiumAsync(User user, bool? endOfPeriod = null)
        {
            var eop = endOfPeriod.GetValueOrDefault(true);
            if(!endOfPeriod.HasValue && user.PremiumExpirationDate.HasValue &&
                user.PremiumExpirationDate.Value < DateTime.UtcNow)
            {
                eop = false;
            }
            await _paymentService.CancelSubscriptionAsync(user, eop);
        }

        public async Task ReinstatePremiumAsync(User user)
        {
            await _paymentService.ReinstateSubscriptionAsync(user);
        }

        public async Task DisablePremiumAsync(Guid userId, DateTime? expirationDate)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            await DisablePremiumAsync(user, expirationDate);
        }

        public async Task DisablePremiumAsync(User user, DateTime? expirationDate)
        {
            if(user != null && user.Premium)
            {
                user.Premium = false;
                user.PremiumExpirationDate = expirationDate;
                user.RevisionDate = DateTime.UtcNow;
                await _userRepository.ReplaceAsync(user);
            }
        }

        public async Task UpdatePremiumExpirationAsync(Guid userId, DateTime? expirationDate)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if(user != null)
            {
                user.PremiumExpirationDate = expirationDate;
                user.RevisionDate = DateTime.UtcNow;
                await _userRepository.ReplaceAsync(user);
            }
        }

        public async Task<UserLicense> GenerateLicenseAsync(User user, SubscriptionInfo subscriptionInfo = null)
        {
            if(user == null)
            {
                throw new NotFoundException();
            }

            if(subscriptionInfo == null && user.Gateway != null)
            {
                subscriptionInfo = await _paymentService.GetSubscriptionAsync(user);
            }

            return subscriptionInfo == null ? new UserLicense(user, _licenseService) :
                new UserLicense(user, subscriptionInfo, _licenseService);
        }

        public override async Task<bool> CheckPasswordAsync(User user, string password)
        {
            if(user == null)
            {
                return false;
            }

            var result = await base.VerifyPasswordAsync(Store as IUserPasswordStore<User>, user, password);
            if(result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                await UpdatePasswordHash(user, password, false, false);
                user.RevisionDate = DateTime.UtcNow;
                await _userRepository.ReplaceAsync(user);
            }

            var success = result != PasswordVerificationResult.Failed;
            if(!success)
            {
                Logger.LogWarning(0, "Invalid password for user {userId}.", user.Id);
            }
            return success;
        }

        public async Task<bool> CanAccessPremium(ITwoFactorProvidersUser user)
        {
            var userId = user.GetUserId();
            if(!userId.HasValue)
            {
                return false;
            }
            if(user.GetPremium())
            {
                return true;
            }
            var orgs = await _currentContext.OrganizationMembershipAsync(_organizationUserRepository, userId.Value);
            if(!orgs.Any())
            {
                return false;
            }
            var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
            return orgs.Any(o => orgAbilities.ContainsKey(o.Id) &&
                orgAbilities[o.Id].UsersGetPremium && orgAbilities[o.Id].Enabled);
        }

        public async Task<bool> TwoFactorIsEnabledAsync(ITwoFactorProvidersUser user)
        {
            var providers = user.GetTwoFactorProviders();
            if(providers == null)
            {
                return false;
            }

            foreach(var p in providers)
            {
                if(p.Value?.Enabled ?? false)
                {
                    if(!TwoFactorProvider.RequiresPremium(p.Key))
                    {
                        return true;
                    }
                    if(await CanAccessPremium(user))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public async Task<bool> TwoFactorProviderIsEnabledAsync(TwoFactorProviderType provider, ITwoFactorProvidersUser user)
        {
            var providers = user.GetTwoFactorProviders();
            if(providers == null || !providers.ContainsKey(provider) || !providers[provider].Enabled)
            {
                return false;
            }

            if(!TwoFactorProvider.RequiresPremium(provider))
            {
                return true;
            }

            return await CanAccessPremium(user);
        }

        private async Task<IdentityResult> UpdatePasswordHash(User user, string newPassword,
            bool validatePassword = true, bool refreshStamp = true)
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
            if(refreshStamp)
            {
                user.SecurityStamp = Guid.NewGuid().ToString();
            }

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
