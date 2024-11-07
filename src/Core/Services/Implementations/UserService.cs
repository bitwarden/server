using System.Security.Claims;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Billing.Models.Sales;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Bit.Core.Vault.Repositories;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using File = System.IO.File;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Bit.Core.Services;

public class UserService : UserManager<User>, IUserService, IDisposable
{
    private const string PremiumPlanId = "premium-annually";
    private const string StoragePlanId = "storage-gb-annually";

    private readonly IUserRepository _userRepository;
    private readonly ICipherRepository _cipherRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationRepository _organizationRepository;
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
    private readonly IPolicyRepository _policyRepository;
    private readonly IPolicyService _policyService;
    private readonly IDataProtector _organizationServiceDataProtector;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IFido2 _fido2;
    private readonly ICurrentContext _currentContext;
    private readonly IGlobalSettings _globalSettings;
    private readonly IAcceptOrgUserCommand _acceptOrgUserCommand;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IStripeSyncService _stripeSyncService;
    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory;
    private readonly IFeatureService _featureService;
    private readonly IPremiumUserBillingService _premiumUserBillingService;
    private readonly IRemoveOrganizationUserCommand _removeOrganizationUserCommand;

    public UserService(
        IUserRepository userRepository,
        ICipherRepository cipherRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
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
        IPolicyRepository policyRepository,
        IPolicyService policyService,
        IReferenceEventService referenceEventService,
        IFido2 fido2,
        ICurrentContext currentContext,
        IGlobalSettings globalSettings,
        IAcceptOrgUserCommand acceptOrgUserCommand,
        IProviderUserRepository providerUserRepository,
        IStripeSyncService stripeSyncService,
        IDataProtectorTokenFactory<OrgUserInviteTokenable> orgUserInviteTokenDataFactory,
        IFeatureService featureService,
        IPremiumUserBillingService premiumUserBillingService,
        IRemoveOrganizationUserCommand removeOrganizationUserCommand)
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
        _policyRepository = policyRepository;
        _policyService = policyService;
        _organizationServiceDataProtector = dataProtectionProvider.CreateProtector(
            "OrganizationServiceDataProtector");
        _referenceEventService = referenceEventService;
        _fido2 = fido2;
        _currentContext = currentContext;
        _globalSettings = globalSettings;
        _acceptOrgUserCommand = acceptOrgUserCommand;
        _providerUserRepository = providerUserRepository;
        _stripeSyncService = stripeSyncService;
        _orgUserInviteTokenDataFactory = orgUserInviteTokenDataFactory;
        _featureService = featureService;
        _premiumUserBillingService = premiumUserBillingService;
        _removeOrganizationUserCommand = removeOrganizationUserCommand;
    }

    public Guid? GetProperUserId(ClaimsPrincipal principal)
    {
        if (!Guid.TryParse(GetUserId(principal), out var userIdGuid))
        {
            return null;
        }

        return userIdGuid;
    }

    public async Task<User> GetUserByIdAsync(string userId)
    {
        if (_currentContext?.User != null &&
            string.Equals(_currentContext.User.Id.ToString(), userId, StringComparison.InvariantCultureIgnoreCase))
        {
            return _currentContext.User;
        }

        if (!Guid.TryParse(userId, out var userIdGuid))
        {
            return null;
        }

        _currentContext.User = await _userRepository.GetByIdAsync(userIdGuid);
        return _currentContext.User;
    }

    public async Task<User> GetUserByIdAsync(Guid userId)
    {
        if (_currentContext?.User != null && _currentContext.User.Id == userId)
        {
            return _currentContext.User;
        }

        _currentContext.User = await _userRepository.GetByIdAsync(userId);
        return _currentContext.User;
    }

    public async Task<User> GetUserByPrincipalAsync(ClaimsPrincipal principal)
    {
        var userId = GetProperUserId(principal);
        if (!userId.HasValue)
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
        if (user.Id == default(Guid))
        {
            throw new ApplicationException("Use register method to create a new user.");
        }

        // if the name is empty, set it to null
        if (String.Equals(user.Name, String.Empty))
        {
            user.Name = null;
        }

        user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
        await _userRepository.ReplaceAsync(user);

        if (push)
        {
            // push
            await _pushService.PushSyncSettingsAsync(user.Id);
        }
    }

    public override async Task<IdentityResult> DeleteAsync(User user)
    {
        // Check if user is the only owner of any organizations.
        var onlyOwnerCount = await _organizationUserRepository.GetCountByOnlyOwnerAsync(user.Id);
        if (onlyOwnerCount > 0)
        {
            var deletedOrg = false;
            var orgs = await _organizationUserRepository.GetManyDetailsByUserAsync(user.Id,
                OrganizationUserStatusType.Confirmed);
            if (orgs.Count == 1)
            {
                var org = await _organizationRepository.GetByIdAsync(orgs.First().OrganizationId);
                if (org != null && (!org.Enabled || string.IsNullOrWhiteSpace(org.GatewaySubscriptionId)))
                {
                    var orgCount = await _organizationUserRepository.GetCountByOrganizationIdAsync(org.Id);
                    if (orgCount <= 1)
                    {
                        await _organizationRepository.DeleteAsync(org);
                        deletedOrg = true;
                    }
                }
            }

            if (!deletedOrg)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Description = "Cannot delete this user because it is the sole owner of at least one organization. Please delete these organizations or upgrade another user.",
                });
            }
        }

        var onlyOwnerProviderCount = await _providerUserRepository.GetCountByOnlyOwnerAsync(user.Id);
        if (onlyOwnerProviderCount > 0)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Description = "Cannot delete this user because it is the sole owner of at least one provider. Please delete these providers or upgrade another user.",
            });
        }

        if (!string.IsNullOrWhiteSpace(user.GatewaySubscriptionId))
        {
            try
            {
                await CancelPremiumAsync(user);
            }
            catch (GatewayException) { }
        }

        await _userRepository.DeleteAsync(user);
        await _referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.DeleteAccount, user, _currentContext));
        await _pushService.PushLogOutAsync(user.Id);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(User user, string token)
    {
        if (!(await VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, "DeleteAccount", token)))
        {
            return IdentityResult.Failed(ErrorDescriber.InvalidToken());
        }

        return await DeleteAsync(user);
    }

    public async Task SendDeleteConfirmationAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
        {
            // No user exists.
            return;
        }

        if (await IsManagedByAnyOrganizationAsync(user.Id))
        {
            await _mailService.SendCannotDeleteManagedAccountEmailAsync(user.Email);
            return;
        }

        var token = await base.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, "DeleteAccount");
        await _mailService.SendVerifyDeleteEmailAsync(user.Email, user.Id, token);
    }

    public async Task<IdentityResult> CreateUserAsync(User user)
    {
        return await CreateAsync(user);
    }

    public async Task<IdentityResult> CreateUserAsync(User user, string masterPasswordHash)
    {
        return await CreateAsync(user, masterPasswordHash);
    }

    public async Task SendMasterPasswordHintAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
        {
            // No user exists. Do we want to send an email telling them this in the future?
            return;
        }

        if (string.IsNullOrWhiteSpace(user.MasterPasswordHint))
        {
            await _mailService.SendNoMasterPasswordHintEmailAsync(email);
            return;
        }

        await _mailService.SendMasterPasswordHintEmailAsync(email, user.MasterPasswordHint);
    }

    public async Task SendTwoFactorEmailAsync(User user)
    {
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Email);
        if (provider == null || provider.MetaData == null || !provider.MetaData.ContainsKey("Email"))
        {
            throw new ArgumentNullException("No email.");
        }

        var email = ((string)provider.MetaData["Email"]).ToLowerInvariant();
        var token = await base.GenerateTwoFactorTokenAsync(user,
            CoreHelpers.CustomProviderName(TwoFactorProviderType.Email));
        await _mailService.SendTwoFactorEmailAsync(email, token);
    }

    public async Task<bool> VerifyTwoFactorEmailAsync(User user, string token)
    {
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Email);
        if (provider == null || provider.MetaData == null || !provider.MetaData.ContainsKey("Email"))
        {
            throw new ArgumentNullException("No email.");
        }

        var email = ((string)provider.MetaData["Email"]).ToLowerInvariant();
        return await base.VerifyTwoFactorTokenAsync(user,
            CoreHelpers.CustomProviderName(TwoFactorProviderType.Email), token);
    }

    public async Task<CredentialCreateOptions> StartWebAuthnRegistrationAsync(User user)
    {
        var providers = user.GetTwoFactorProviders();
        if (providers == null)
        {
            providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
        }
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn);
        if (provider == null)
        {
            provider = new TwoFactorProvider
            {
                Enabled = false
            };
        }
        if (provider.MetaData == null)
        {
            provider.MetaData = new Dictionary<string, object>();
        }

        var fidoUser = new Fido2User
        {
            DisplayName = user.Name,
            Name = user.Email,
            Id = user.Id.ToByteArray(),
        };

        var excludeCredentials = provider.MetaData
            .Where(k => k.Key.StartsWith("Key"))
            .Select(k => new TwoFactorProvider.WebAuthnData((dynamic)k.Value).Descriptor)
            .ToList();

        var authenticatorSelection = new AuthenticatorSelection
        {
            AuthenticatorAttachment = null,
            RequireResidentKey = false,
            UserVerification = UserVerificationRequirement.Discouraged
        };
        var options = _fido2.RequestNewCredential(fidoUser, excludeCredentials, authenticatorSelection, AttestationConveyancePreference.None);

        provider.MetaData["pending"] = options.ToJson();
        providers[TwoFactorProviderType.WebAuthn] = provider;
        user.SetTwoFactorProviders(providers);
        await UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn, false);

        return options;
    }

    public async Task<bool> CompleteWebAuthRegistrationAsync(User user, int id, string name, AuthenticatorAttestationRawResponse attestationResponse)
    {
        var keyId = $"Key{id}";

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn);
        if (!provider?.MetaData?.ContainsKey("pending") ?? true)
        {
            return false;
        }

        var options = CredentialCreateOptions.FromJson((string)provider.MetaData["pending"]);

        // Callback to ensure credential ID is unique. Always return true since we don't care if another
        // account uses the same 2FA key.
        IsCredentialIdUniqueToUserAsyncDelegate callback = (args, cancellationToken) => Task.FromResult(true);

        var success = await _fido2.MakeNewCredentialAsync(attestationResponse, options, callback);

        provider.MetaData.Remove("pending");
        provider.MetaData[keyId] = new TwoFactorProvider.WebAuthnData
        {
            Name = name,
            Descriptor = new PublicKeyCredentialDescriptor(success.Result.CredentialId),
            PublicKey = success.Result.PublicKey,
            UserHandle = success.Result.User.Id,
            SignatureCounter = success.Result.Counter,
            CredType = success.Result.CredType,
            RegDate = DateTime.Now,
            AaGuid = success.Result.Aaguid
        };

        var providers = user.GetTwoFactorProviders();
        providers[TwoFactorProviderType.WebAuthn] = provider;
        user.SetTwoFactorProviders(providers);
        await UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn);

        return true;
    }

    public async Task<bool> DeleteWebAuthnKeyAsync(User user, int id)
    {
        var providers = user.GetTwoFactorProviders();
        if (providers == null)
        {
            return false;
        }

        var keyName = $"Key{id}";
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn);
        if (!provider?.MetaData?.ContainsKey(keyName) ?? true)
        {
            return false;
        }

        if (provider.MetaData.Count < 2)
        {
            return false;
        }

        provider.MetaData.Remove(keyName);
        providers[TwoFactorProviderType.WebAuthn] = provider;
        user.SetTwoFactorProviders(providers);
        await UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn);
        return true;
    }

    public async Task SendEmailVerificationAsync(User user)
    {
        if (user.EmailVerified)
        {
            throw new BadRequestException("Email already verified.");
        }

        var token = await base.GenerateEmailConfirmationTokenAsync(user);
        await _mailService.SendVerifyEmailEmailAsync(user.Email, user.Id, token);
    }

    public async Task InitiateEmailChangeAsync(User user, string newEmail)
    {
        var existingUser = await _userRepository.GetByEmailAsync(newEmail);
        if (existingUser != null)
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
        if (verifyPasswordResult == PasswordVerificationResult.Failed)
        {
            return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
        }

        if (!await base.VerifyUserTokenAsync(user, _identityOptions.Tokens.ChangeEmailTokenProvider,
            GetChangeEmailTokenPurpose(newEmail), token))
        {
            return IdentityResult.Failed(_identityErrorDescriber.InvalidToken());
        }

        var existingUser = await _userRepository.GetByEmailAsync(newEmail);
        if (existingUser != null && existingUser.Id != user.Id)
        {
            return IdentityResult.Failed(_identityErrorDescriber.DuplicateEmail(newEmail));
        }

        var previousState = new
        {
            Key = user.Key,
            MasterPassword = user.MasterPassword,
            SecurityStamp = user.SecurityStamp,
            Email = user.Email
        };

        var result = await UpdatePasswordHash(user, newMasterPassword);
        if (!result.Succeeded)
        {
            return result;
        }

        var now = DateTime.UtcNow;

        user.Key = key;
        user.Email = newEmail;
        user.EmailVerified = true;
        user.RevisionDate = user.AccountRevisionDate = now;
        user.LastEmailChangeDate = now;
        await _userRepository.ReplaceAsync(user);

        if (user.Gateway == GatewayType.Stripe)
        {

            try
            {
                await _stripeSyncService.UpdateCustomerEmailAddress(user.GatewayCustomerId,
                    user.BillingEmailAddress());
            }
            catch (Exception ex)
            {
                //if sync to strip fails, update email and securityStamp to previous
                user.Key = previousState.Key;
                user.Email = previousState.Email;
                user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
                user.MasterPassword = previousState.MasterPassword;
                user.SecurityStamp = previousState.SecurityStamp;

                await _userRepository.ReplaceAsync(user);
                return IdentityResult.Failed(new IdentityError
                {
                    Description = ex.Message
                });
            }
        }

        await _pushService.PushLogOutAsync(user.Id);

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> ChangePasswordAsync(User user, string masterPassword, string newMasterPassword, string passwordHint,
        string key)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        if (await CheckPasswordAsync(user, masterPassword))
        {
            var result = await UpdatePasswordHash(user, newMasterPassword);
            if (!result.Succeeded)
            {
                return result;
            }

            var now = DateTime.UtcNow;
            user.RevisionDate = user.AccountRevisionDate = now;
            user.LastPasswordChangeDate = now;
            user.Key = key;
            user.MasterPasswordHint = passwordHint;

            await _userRepository.ReplaceAsync(user);
            await _eventService.LogUserEventAsync(user.Id, EventType.User_ChangedPassword);
            await _pushService.PushLogOutAsync(user.Id, true);

            return IdentityResult.Success;
        }

        Logger.LogWarning("Change password failed for user {userId}.", user.Id);
        return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
    }

    public async Task<IdentityResult> SetKeyConnectorKeyAsync(User user, string key, string orgIdentifier)
    {
        var identityResult = CheckCanUseKeyConnector(user);
        if (identityResult != null)
        {
            return identityResult;
        }

        user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
        user.Key = key;
        user.UsesKeyConnector = true;

        await _userRepository.ReplaceAsync(user);
        await _eventService.LogUserEventAsync(user.Id, EventType.User_MigratedKeyToKeyConnector);

        await _acceptOrgUserCommand.AcceptOrgUserByOrgSsoIdAsync(orgIdentifier, user, this);

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> ConvertToKeyConnectorAsync(User user)
    {
        var identityResult = CheckCanUseKeyConnector(user);
        if (identityResult != null)
        {
            return identityResult;
        }

        user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
        user.MasterPassword = null;
        user.UsesKeyConnector = true;

        await _userRepository.ReplaceAsync(user);
        await _eventService.LogUserEventAsync(user.Id, EventType.User_MigratedKeyToKeyConnector);

        return IdentityResult.Success;
    }

    private IdentityResult CheckCanUseKeyConnector(User user)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        if (user.UsesKeyConnector)
        {
            Logger.LogWarning("Already uses Key Connector.");
            return IdentityResult.Failed(_identityErrorDescriber.UserAlreadyHasPassword());
        }

        if (_currentContext.Organizations.Any(u =>
                u.Type is OrganizationUserType.Owner or OrganizationUserType.Admin))
        {
            throw new BadRequestException("Cannot use Key Connector when admin or owner of an organization.");
        }

        return null;
    }

    public async Task<IdentityResult> AdminResetPasswordAsync(OrganizationUserType callingUserType, Guid orgId, Guid id, string newMasterPassword, string key)
    {
        // Org must be able to use reset password
        var org = await _organizationRepository.GetByIdAsync(orgId);
        if (org == null || !org.UseResetPassword)
        {
            throw new BadRequestException("Organization does not allow password reset.");
        }

        // Enterprise policy must be enabled
        var resetPasswordPolicy =
            await _policyRepository.GetByOrganizationIdTypeAsync(orgId, PolicyType.ResetPassword);
        if (resetPasswordPolicy == null || !resetPasswordPolicy.Enabled)
        {
            throw new BadRequestException("Organization does not have the password reset policy enabled.");
        }

        // Org User must be confirmed and have a ResetPasswordKey
        var orgUser = await _organizationUserRepository.GetByIdAsync(id);
        if (orgUser == null || orgUser.Status != OrganizationUserStatusType.Confirmed ||
            orgUser.OrganizationId != orgId || string.IsNullOrEmpty(orgUser.ResetPasswordKey) ||
            !orgUser.UserId.HasValue)
        {
            throw new BadRequestException("Organization User not valid");
        }

        // Calling User must be of higher/equal user type to reset user's password
        var canAdjustPassword = false;
        switch (callingUserType)
        {
            case OrganizationUserType.Owner:
                canAdjustPassword = true;
                break;
            case OrganizationUserType.Admin:
                canAdjustPassword = orgUser.Type != OrganizationUserType.Owner;
                break;
            case OrganizationUserType.Custom:
                canAdjustPassword = orgUser.Type != OrganizationUserType.Owner &&
                    orgUser.Type != OrganizationUserType.Admin;
                break;
        }

        if (!canAdjustPassword)
        {
            throw new BadRequestException("Calling user does not have permission to reset this user's master password");
        }

        var user = await GetUserByIdAsync(orgUser.UserId.Value);
        if (user == null)
        {
            throw new NotFoundException();
        }

        if (user.UsesKeyConnector)
        {
            throw new BadRequestException("Cannot reset password of a user with Key Connector.");
        }

        var result = await UpdatePasswordHash(user, newMasterPassword);
        if (!result.Succeeded)
        {
            return result;
        }

        user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
        user.LastPasswordChangeDate = user.RevisionDate;
        user.ForcePasswordReset = true;
        user.Key = key;

        await _userRepository.ReplaceAsync(user);
        await _mailService.SendAdminResetPasswordEmailAsync(user.Email, user.Name, org.DisplayName());
        await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_AdminResetPassword);
        await _pushService.PushLogOutAsync(user.Id);

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateTempPasswordAsync(User user, string newMasterPassword, string key, string hint)
    {
        if (!user.ForcePasswordReset)
        {
            throw new BadRequestException("User does not have a temporary password to update.");
        }

        var result = await UpdatePasswordHash(user, newMasterPassword);
        if (!result.Succeeded)
        {
            return result;
        }

        user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
        user.ForcePasswordReset = false;
        user.Key = key;
        user.MasterPasswordHint = hint;

        await _userRepository.ReplaceAsync(user);
        await _mailService.SendUpdatedTempPasswordEmailAsync(user.Email, user.Name);
        await _eventService.LogUserEventAsync(user.Id, EventType.User_UpdatedTempPassword);
        await _pushService.PushLogOutAsync(user.Id);

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> ChangeKdfAsync(User user, string masterPassword, string newMasterPassword,
        string key, KdfType kdf, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        if (await CheckPasswordAsync(user, masterPassword))
        {
            var result = await UpdatePasswordHash(user, newMasterPassword);
            if (!result.Succeeded)
            {
                return result;
            }

            var now = DateTime.UtcNow;
            user.RevisionDate = user.AccountRevisionDate = now;
            user.LastKdfChangeDate = now;
            user.Key = key;
            user.Kdf = kdf;
            user.KdfIterations = kdfIterations;
            user.KdfMemory = kdfMemory;
            user.KdfParallelism = kdfParallelism;
            await _userRepository.ReplaceAsync(user);
            await _pushService.PushLogOutAsync(user.Id);
            return IdentityResult.Success;
        }

        Logger.LogWarning("Change KDF failed for user {userId}.", user.Id);
        return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
    }

    public async Task<IdentityResult> RefreshSecurityStampAsync(User user, string secret)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        if (await VerifySecretAsync(user, secret))
        {
            var result = await base.UpdateSecurityStampAsync(user);
            if (!result.Succeeded)
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

    public async Task UpdateTwoFactorProviderAsync(User user, TwoFactorProviderType type, bool setEnabled = true, bool logEvent = true)
    {
        SetTwoFactorProvider(user, type, setEnabled);
        await SaveUserAsync(user);
        if (logEvent)
        {
            await _eventService.LogUserEventAsync(user.Id, EventType.User_Updated2fa);
        }
    }

    public async Task DisableTwoFactorProviderAsync(User user, TwoFactorProviderType type)
    {
        var providers = user.GetTwoFactorProviders();
        if (!providers?.ContainsKey(type) ?? true)
        {
            return;
        }

        providers.Remove(type);
        user.SetTwoFactorProviders(providers);
        await SaveUserAsync(user);
        await _eventService.LogUserEventAsync(user.Id, EventType.User_Disabled2fa);

        if (!await TwoFactorIsEnabledAsync(user))
        {
            await CheckPoliciesOnTwoFactorRemovalAsync(user);
        }
    }

    public async Task<bool> RecoverTwoFactorAsync(string email, string secret, string recoveryCode)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
        {
            // No user exists. Do we want to send an email telling them this in the future?
            return false;
        }

        if (!await VerifySecretAsync(user, secret))
        {
            return false;
        }

        if (!CoreHelpers.FixedTimeEquals(user.TwoFactorRecoveryCode, recoveryCode))
        {
            return false;
        }

        user.TwoFactorProviders = null;
        user.TwoFactorRecoveryCode = CoreHelpers.SecureRandomString(32, upper: false, special: false);
        await SaveUserAsync(user);
        await _mailService.SendRecoverTwoFactorEmail(user.Email, DateTime.UtcNow, _currentContext.IpAddress);
        await _eventService.LogUserEventAsync(user.Id, EventType.User_Recovered2fa);
        await CheckPoliciesOnTwoFactorRemovalAsync(user);

        return true;
    }

    public async Task<Tuple<bool, string>> SignUpPremiumAsync(User user, string paymentToken,
        PaymentMethodType paymentMethodType, short additionalStorageGb, UserLicense license,
        TaxInfo taxInfo)
    {
        if (user.Premium)
        {
            throw new BadRequestException("Already a premium user.");
        }

        if (additionalStorageGb < 0)
        {
            throw new BadRequestException("You can't subtract storage!");
        }

        string paymentIntentClientSecret = null;
        IPaymentService paymentService = null;
        if (_globalSettings.SelfHosted)
        {
            if (license == null || !_licenseService.VerifyLicense(license))
            {
                throw new BadRequestException("Invalid license.");
            }

            var claimsPrincipal = _licenseService.GetClaimsPrincipalFromLicense(license);

            if (!license.CanUse(user, claimsPrincipal, out var exceptionMessage))
            {
                throw new BadRequestException(exceptionMessage);
            }

            var dir = $"{_globalSettings.LicenseDirectory}/user";
            Directory.CreateDirectory(dir);
            using var fs = File.OpenWrite(Path.Combine(dir, $"{user.Id}.json"));
            await JsonSerializer.SerializeAsync(fs, license, JsonHelpers.Indented);
        }
        else
        {
            var deprecateStripeSourcesAPI = _featureService.IsEnabled(FeatureFlagKeys.AC2476_DeprecateStripeSourcesAPI);

            if (deprecateStripeSourcesAPI)
            {
                var sale = PremiumUserSale.From(user, paymentMethodType, paymentToken, taxInfo, additionalStorageGb);
                await _premiumUserBillingService.Finalize(sale);
            }
            else
            {
                paymentIntentClientSecret = await _paymentService.PurchasePremiumAsync(user, paymentMethodType,
                    paymentToken, additionalStorageGb, taxInfo);
            }
        }

        user.Premium = true;
        user.RevisionDate = DateTime.UtcNow;

        if (_globalSettings.SelfHosted)
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
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.UpgradePlan, user, _currentContext)
                {
                    Storage = user.MaxStorageGb,
                    PlanName = PremiumPlanId,
                });
        }
        catch when (!_globalSettings.SelfHosted)
        {
            await paymentService.CancelAndRecoverChargesAsync(user);
            throw;
        }
        return new Tuple<bool, string>(string.IsNullOrWhiteSpace(paymentIntentClientSecret),
            paymentIntentClientSecret);
    }

    public async Task UpdateLicenseAsync(User user, UserLicense license)
    {
        if (!_globalSettings.SelfHosted)
        {
            throw new InvalidOperationException("Licenses require self hosting.");
        }

        if (license?.LicenseType != null && license.LicenseType != LicenseType.User)
        {
            throw new BadRequestException("Organization licenses cannot be applied to a user. "
                + "Upload this license from the Organization settings page.");
        }

        if (license == null || !_licenseService.VerifyLicense(license))
        {
            throw new BadRequestException("Invalid license.");
        }

        var claimsPrincipal = _licenseService.GetClaimsPrincipalFromLicense(license);

        if (!license.CanUse(user, claimsPrincipal, out var exceptionMessage))
        {
            throw new BadRequestException(exceptionMessage);
        }

        var dir = $"{_globalSettings.LicenseDirectory}/user";
        Directory.CreateDirectory(dir);
        using var fs = File.OpenWrite(Path.Combine(dir, $"{user.Id}.json"));
        await JsonSerializer.SerializeAsync(fs, license, JsonHelpers.Indented);

        user.Premium = license.Premium;
        user.RevisionDate = DateTime.UtcNow;
        user.MaxStorageGb = _globalSettings.SelfHosted ? 10240 : license.MaxStorageGb; // 10 TB
        user.LicenseKey = license.LicenseKey;
        user.PremiumExpirationDate = license.Expires;
        await SaveUserAsync(user);
    }

    public async Task<string> AdjustStorageAsync(User user, short storageAdjustmentGb)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        if (!user.Premium)
        {
            throw new BadRequestException("Not a premium user.");
        }

        var secret = await BillingHelpers.AdjustStorageAsync(_paymentService, user, storageAdjustmentGb,
            StoragePlanId);
        await _referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.AdjustStorage, user, _currentContext)
            {
                Storage = storageAdjustmentGb,
                PlanName = StoragePlanId,
            });
        await SaveUserAsync(user);
        return secret;
    }

    public async Task ReplacePaymentMethodAsync(User user, string paymentToken, PaymentMethodType paymentMethodType, TaxInfo taxInfo)
    {
        if (paymentToken.StartsWith("btok_"))
        {
            throw new BadRequestException("Invalid token.");
        }

        var updated = await _paymentService.UpdatePaymentMethodAsync(user, paymentMethodType, paymentToken, taxInfo: taxInfo);
        if (updated)
        {
            await SaveUserAsync(user);
        }
    }

    public async Task CancelPremiumAsync(User user, bool? endOfPeriod = null)
    {
        var eop = endOfPeriod.GetValueOrDefault(true);
        if (!endOfPeriod.HasValue && user.PremiumExpirationDate.HasValue &&
            user.PremiumExpirationDate.Value < DateTime.UtcNow)
        {
            eop = false;
        }
        await _paymentService.CancelSubscriptionAsync(user, eop);
        await _referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.CancelSubscription, user, _currentContext)
            {
                EndOfPeriod = eop
            });
    }

    public async Task ReinstatePremiumAsync(User user)
    {
        await _paymentService.ReinstateSubscriptionAsync(user);
        await _referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.ReinstateSubscription, user, _currentContext));
    }

    public async Task EnablePremiumAsync(Guid userId, DateTime? expirationDate)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        await EnablePremiumAsync(user, expirationDate);
    }

    public async Task EnablePremiumAsync(User user, DateTime? expirationDate)
    {
        if (user != null && !user.Premium && user.Gateway.HasValue)
        {
            user.Premium = true;
            user.PremiumExpirationDate = expirationDate;
            user.RevisionDate = DateTime.UtcNow;
            await _userRepository.ReplaceAsync(user);
        }
    }

    public async Task DisablePremiumAsync(Guid userId, DateTime? expirationDate)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        await DisablePremiumAsync(user, expirationDate);
    }

    public async Task DisablePremiumAsync(User user, DateTime? expirationDate)
    {
        if (user != null && user.Premium)
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
        if (user != null)
        {
            user.PremiumExpirationDate = expirationDate;
            user.RevisionDate = DateTime.UtcNow;
            await _userRepository.ReplaceAsync(user);
        }
    }

    public async Task<UserLicense> GenerateLicenseAsync(
        User user,
        SubscriptionInfo subscriptionInfo = null,
        int? version = null)
    {
        if (user == null)
        {
            throw new NotFoundException();
        }

        if (subscriptionInfo == null && user.Gateway != null)
        {
            subscriptionInfo = await _paymentService.GetSubscriptionAsync(user);
        }

        var userLicense = subscriptionInfo == null
            ? new UserLicense(user, _licenseService)
            : new UserLicense(user, subscriptionInfo, _licenseService);

        userLicense.Token = await _licenseService.CreateUserTokenAsync(user, subscriptionInfo);

        return userLicense;
    }

    public override async Task<bool> CheckPasswordAsync(User user, string password)
    {
        if (user == null)
        {
            return false;
        }

        var result = await base.VerifyPasswordAsync(Store as IUserPasswordStore<User>, user, password);
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            await UpdatePasswordHash(user, password, false, false);
            user.RevisionDate = DateTime.UtcNow;
            await _userRepository.ReplaceAsync(user);
        }

        var success = result != PasswordVerificationResult.Failed;
        if (!success)
        {
            Logger.LogWarning(0, "Invalid password for user {userId}.", user.Id);
        }
        return success;
    }

    public async Task<bool> CanAccessPremium(ITwoFactorProvidersUser user)
    {
        var userId = user.GetUserId();
        if (!userId.HasValue)
        {
            return false;
        }

        return user.GetPremium() || await this.HasPremiumFromOrganization(user);
    }

    public async Task<bool> HasPremiumFromOrganization(ITwoFactorProvidersUser user)
    {
        var userId = user.GetUserId();
        if (!userId.HasValue)
        {
            return false;
        }

        // orgUsers in the Invited status are not associated with a userId yet, so this will get
        // orgUsers in Accepted and Confirmed states only
        var orgUsers = await _organizationUserRepository.GetManyByUserAsync(userId.Value);

        if (!orgUsers.Any())
        {
            return false;
        }

        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        return orgUsers.Any(ou =>
            orgAbilities.TryGetValue(ou.OrganizationId, out var orgAbility) &&
            orgAbility.UsersGetPremium &&
            orgAbility.Enabled);
    }

    public async Task<bool> TwoFactorIsEnabledAsync(ITwoFactorProvidersUser user)
    {
        var providers = user.GetTwoFactorProviders();
        if (providers == null)
        {
            return false;
        }

        foreach (var p in providers)
        {
            if (p.Value?.Enabled ?? false)
            {
                if (!TwoFactorProvider.RequiresPremium(p.Key))
                {
                    return true;
                }
                if (await CanAccessPremium(user))
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
        if (providers == null || !providers.ContainsKey(provider) || !providers[provider].Enabled)
        {
            return false;
        }

        if (!TwoFactorProvider.RequiresPremium(provider))
        {
            return true;
        }

        return await CanAccessPremium(user);
    }

    public async Task<string> GenerateSignInTokenAsync(User user, string purpose)
    {
        var token = await GenerateUserTokenAsync(user, Options.Tokens.PasswordResetTokenProvider,
            purpose);
        return token;
    }

    public async Task<IdentityResult> UpdatePasswordHash(User user, string newPassword,
        bool validatePassword = true, bool refreshStamp = true)
    {
        if (validatePassword)
        {
            var validate = await ValidatePasswordInternal(user, newPassword);
            if (!validate.Succeeded)
            {
                return validate;
            }
        }

        user.MasterPassword = _passwordHasher.HashPassword(user, newPassword);
        if (refreshStamp)
        {
            user.SecurityStamp = Guid.NewGuid().ToString();
        }

        return IdentityResult.Success;
    }

    public async Task<bool> IsLegacyUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var user = await FindByIdAsync(userId);
        if (user == null)
        {
            return false;
        }

        return IsLegacyUser(user);
    }

    public async Task<bool> IsManagedByAnyOrganizationAsync(Guid userId)
    {
        var managingOrganizations = await GetOrganizationsManagingUserAsync(userId);
        return managingOrganizations.Any();
    }

    public async Task<IEnumerable<Organization>> GetOrganizationsManagingUserAsync(Guid userId)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning))
        {
            return Enumerable.Empty<Organization>();
        }

        // Get all organizations that have verified the user's email domain.
        var organizationsWithVerifiedUserEmailDomain = await _organizationRepository.GetByVerifiedUserEmailDomainAsync(userId);

        // Organizations must be enabled and able to have verified domains.
        // TODO: Replace "UseSso" with a new organization ability like "UseOrganizationDomains" (PM-11622).
        // Verified domains were tied to SSO, so we currently check the "UseSso" organization ability.
        return organizationsWithVerifiedUserEmailDomain.Where(organization => organization is { Enabled: true, UseSso: true });
    }

    /// <inheritdoc cref="IsLegacyUser(string)"/>
    public static bool IsLegacyUser(User user)
    {
        return user.Key == null && user.MasterPassword != null && user.PrivateKey != null;
    }

    private async Task<IdentityResult> ValidatePasswordInternal(User user, string password)
    {
        var errors = new List<IdentityError>();
        foreach (var v in _passwordValidators)
        {
            var result = await v.ValidateAsync(this, user, password);
            if (!result.Succeeded)
            {
                errors.AddRange(result.Errors);
            }
        }

        if (errors.Count > 0)
        {
            Logger.LogWarning("User {userId} password validation failed: {errors}.", await GetUserIdAsync(user),
                string.Join(";", errors.Select(e => e.Code)));
            return IdentityResult.Failed(errors.ToArray());
        }

        return IdentityResult.Success;
    }

    public void SetTwoFactorProvider(User user, TwoFactorProviderType type, bool setEnabled = true)
    {
        var providers = user.GetTwoFactorProviders();
        if (!providers?.ContainsKey(type) ?? true)
        {
            return;
        }

        if (setEnabled)
        {
            providers[type].Enabled = true;
        }
        user.SetTwoFactorProviders(providers);

        if (string.IsNullOrWhiteSpace(user.TwoFactorRecoveryCode))
        {
            user.TwoFactorRecoveryCode = CoreHelpers.SecureRandomString(32, upper: false, special: false);
        }
    }

    private async Task CheckPoliciesOnTwoFactorRemovalAsync(User user)
    {
        var twoFactorPolicies = await _policyService.GetPoliciesApplicableToUserAsync(user.Id, PolicyType.TwoFactorAuthentication);

        var removeOrgUserTasks = twoFactorPolicies.Select(async p =>
        {
            await _removeOrganizationUserCommand.RemoveUserAsync(p.OrganizationId, user.Id);
            var organization = await _organizationRepository.GetByIdAsync(p.OrganizationId);
            await _mailService.SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(
                organization.DisplayName(), user.Email);
        }).ToArray();

        await Task.WhenAll(removeOrgUserTasks);
    }

    public override async Task<IdentityResult> ConfirmEmailAsync(User user, string token)
    {
        var result = await base.ConfirmEmailAsync(user, token);
        if (result.Succeeded)
        {
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.ConfirmEmailAddress, user, _currentContext));
        }
        return result;
    }

    public async Task RotateApiKeyAsync(User user)
    {
        user.ApiKey = CoreHelpers.SecureRandomString(30);
        user.RevisionDate = DateTime.UtcNow;
        await _userRepository.ReplaceAsync(user);
    }

    public async Task SendOTPAsync(User user)
    {
        if (user.Email == null)
        {
            throw new BadRequestException("No user email.");
        }

        var token = await base.GenerateUserTokenAsync(user, TokenOptions.DefaultEmailProvider,
            "otp:" + user.Email);
        await _mailService.SendOTPEmailAsync(user.Email, token);
    }

    public async Task<bool> VerifyOTPAsync(User user, string token)
    {
        return await base.VerifyUserTokenAsync(user, TokenOptions.DefaultEmailProvider,
            "otp:" + user.Email, token);
    }

    public async Task<bool> VerifySecretAsync(User user, string secret, bool isSettingMFA = false)
    {
        bool isVerified;
        if (user.HasMasterPassword())
        {
            // If the user has a master password the secret is most likely going to be a hash
            // of their password, but in certain scenarios, like when the user has logged into their
            // device without a password (trusted device encryption) but the account
            // does still have a password we will allow the use of OTP.
            isVerified = await CheckPasswordAsync(user, secret) ||
                await VerifyOTPAsync(user, secret);
        }
        else if (isSettingMFA)
        {
            // this is temporary to allow users to view their MFA settings without invalidating email TOTP
            // Will be removed with PM-9925
            isVerified = true;
        }
        else
        {
            // If they don't have a password at all they can only do OTP
            isVerified = await VerifyOTPAsync(user, secret);
        }

        return isVerified;
    }

    private async Task SendAppropriateWelcomeEmailAsync(User user, string initiationPath)
    {
        var isFromMarketingWebsite = initiationPath.Contains("Secrets Manager trial");

        if (isFromMarketingWebsite)
        {
            await _mailService.SendTrialInitiationEmailAsync(user.Email);
        }
        else
        {
            await _mailService.SendWelcomeEmailAsync(user);
        }
    }
}
