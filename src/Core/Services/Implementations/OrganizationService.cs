using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Repositories;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;
using Bit.Core.Utilities;
using Bit.Core.Exceptions;
using System.Collections.Generic;
using Microsoft.AspNetCore.DataProtection;
using Stripe;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using System.IO;
using Newtonsoft.Json;
using Bit.Core.Context;
using Bit.Core.OrganizationFeatures;
using Microsoft.Extensions.Logging;
using Bit.Core.OrganizationFeatures.Mail;

namespace Bit.Core.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly IOrganizationAccessPolicies _organizationAccessPolicies;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ICollectionRepository _collectionRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IPushRegistrationService _pushRegistrationService;
        private readonly IDeviceRepository _deviceRepository;
        private readonly ILicensingService _licensingService;
        private readonly IEventService _eventService;
        private readonly IInstallationRepository _installationRepository;
        private readonly IApplicationCacheService _applicationCacheService;
        private readonly IPaymentService _paymentService;
        private readonly IPolicyRepository _policyRepository;
        private readonly ISsoConfigRepository _ssoConfigRepository;
        private readonly ISsoUserRepository _ssoUserRepository;
        private readonly IReferenceEventService _referenceEventService;
        private readonly IGlobalSettings _globalSettings;
        private readonly ICurrentContext _currentContext;
        private readonly IOrganizationUserMailer _organizationUserMailer;

        public OrganizationService(
            IOrganizationAccessPolicies organizationAccessPolicies,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            ICollectionRepository collectionRepository,
            IUserRepository userRepository,
            IGroupRepository groupRepository,
            IDataProtectionProvider dataProtectionProvider,
            IMailService mailService,
            IPushNotificationService pushNotificationService,
            IPushRegistrationService pushRegistrationService,
            IDeviceRepository deviceRepository,
            ILicensingService licensingService,
            IEventService eventService,
            IInstallationRepository installationRepository,
            IApplicationCacheService applicationCacheService,
            IPaymentService paymentService,
            IPolicyRepository policyRepository,
            ISsoConfigRepository ssoConfigRepository,
            ISsoUserRepository ssoUserRepository,
            IReferenceEventService referenceEventService,
            IGlobalSettings globalSettings,
            ITaxRateRepository taxRateRepository,
            ICurrentContext currentContext,
            ILogger<OrganizationService> logger,
            IOrganizationUserMailer organizationUserMailer)
        {
            _organizationAccessPolicies = organizationAccessPolicies;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _collectionRepository = collectionRepository;
            _groupRepository = groupRepository;
            _pushNotificationService = pushNotificationService;
            _pushRegistrationService = pushRegistrationService;
            _deviceRepository = deviceRepository;
            _licensingService = licensingService;
            _eventService = eventService;
            _installationRepository = installationRepository;
            _applicationCacheService = applicationCacheService;
            _paymentService = paymentService;
            _policyRepository = policyRepository;
            _ssoConfigRepository = ssoConfigRepository;
            _ssoUserRepository = ssoUserRepository;
            _referenceEventService = referenceEventService;
            _globalSettings = globalSettings;
            _currentContext = currentContext;
            _organizationUserMailer = organizationUserMailer;
        }

        // TODO: This feels like it likely doesn't belong. Make OrganizationPaymentService?
        public async Task ReplacePaymentMethodAsync(Guid organizationId, string paymentToken,
            PaymentMethodType paymentMethodType, TaxInfo taxInfo)
        {
            var organization = await GetOrgById(organizationId);
            CoreHelpers.HandlePermissionResult(
                _organizationAccessPolicies.CanReplacePaymentMethod(organization)
            );

            await _paymentService.SaveTaxInfoAsync(organization, taxInfo);
            var updated = await _paymentService.UpdatePaymentMethodAsync(organization,
                paymentMethodType, paymentToken);
            if (updated)
            {
                await ReplaceAndUpdateCache(organization);
            }
        }

        // TODO: This feels like it likely doesn't belong. Make OrganizationPaymentService?
        public async Task VerifyBankAsync(Guid organizationId, int amount1, int amount2)
        {
            var organization = await GetOrgById(organizationId);
            var accessResult = _organizationAccessPolicies.CanVerifyBank(organization);
            if (!accessResult.Permit)
            {
                throw string.IsNullOrWhiteSpace(accessResult.BlockReason)
                    ? new NotFoundException()
                    : new GatewayException(accessResult.BlockReason);
            }

            var bankService = new BankAccountService();
            var customerService = new CustomerService();
            var customer = await customerService.GetAsync(organization.GatewayCustomerId);
            if (customer == null)
            {
                throw new GatewayException("Cannot find customer.");
            }

            var bankAccount = customer.Sources
                    .FirstOrDefault(s => s is BankAccount && ((BankAccount)s).Status != "verified") as BankAccount;
            if (bankAccount == null)
            {
                throw new GatewayException("Cannot find an unverified bank account.");
            }

            try
            {
                var result = await bankService.VerifyAsync(organization.GatewayCustomerId, bankAccount.Id,
                    new BankAccountVerifyOptions { Amounts = new List<long> { amount1, amount2 } });
                if (result.Status != "verified")
                {
                    throw new GatewayException("Unable to verify account.");
                }
            }
            catch (StripeException e)
            {
                throw new GatewayException(e.Message);
            }
        }

        public async Task<Tuple<Organization, OrganizationUser>> SignUpAsync(OrganizationSignup signup,
            bool provider = false)
        {
            var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == signup.Plan);
            CoreHelpers.HandlePermissionResult(
                await _organizationAccessPolicies.CanSignUp(signup, plan, provider)
            );

            var organization = new Organization
            {
                // Pre-generate the org id so that we can save it with the Stripe subscription..
                Id = CoreHelpers.GenerateComb(),
                Name = signup.Name,
                BillingEmail = signup.BillingEmail,
                BusinessName = signup.BusinessName,
                PlanType = plan.Type,
                Seats = (short)(plan.BaseSeats + signup.AdditionalSeats),
                MaxCollections = plan.MaxCollections,
                MaxStorageGb = !plan.BaseStorageGb.HasValue ?
                    (short?)null : (short)(plan.BaseStorageGb.Value + signup.AdditionalStorageGb),
                UsePolicies = plan.HasPolicies,
                UseSso = plan.HasSso,
                UseGroups = plan.HasGroups,
                UseEvents = plan.HasEvents,
                UseDirectory = plan.HasDirectory,
                UseTotp = plan.HasTotp,
                Use2fa = plan.Has2fa,
                UseApi = plan.HasApi,
                UseResetPassword = plan.HasResetPassword,
                SelfHost = plan.HasSelfHost,
                UsersGetPremium = plan.UsersGetPremium || signup.PremiumAccessAddon,
                Plan = plan.Name,
                Gateway = null,
                ReferenceData = signup.Owner.ReferenceData,
                Enabled = true,
                LicenseKey = CoreHelpers.SecureRandomString(20),
                ApiKey = CoreHelpers.SecureRandomString(30),
                PublicKey = signup.PublicKey,
                PrivateKey = signup.PrivateKey,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow,
            };

            if (plan.Type != PlanType.Free)
            {
                await _paymentService.PurchaseOrganizationAsync(organization, signup.PaymentMethodType.Value,
                    signup.PaymentToken, plan, signup.AdditionalStorageGb, signup.AdditionalSeats,
                    signup.PremiumAccessAddon, signup.TaxInfo);
            }

            var ownerId = provider ? default : signup.Owner.Id;
            var returnValue = await SignUpAsync(organization, ownerId, signup.OwnerKey, signup.CollectionName, true);
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.Signup, organization)
                {
                    PlanName = plan.Name,
                    PlanType = plan.Type,
                    Seats = returnValue.Item1.Seats,
                    Storage = returnValue.Item1.MaxStorageGb,
                });
            return returnValue;
        }
        
        public async Task<Tuple<Organization, OrganizationUser>> SelfHostedSignUpAsync(
            OrganizationLicense license, User owner, string ownerKey, string collectionName, string publicKey,
            string privateKey)
        {
            CoreHelpers.HandlePermissionResult(await _organizationAccessPolicies.CanSelfHostedSignUpAsync(license, owner));

            var organization = new Organization
            {
                Name = license.Name,
                BillingEmail = license.BillingEmail,
                BusinessName = license.BusinessName,
                PlanType = license.PlanType,
                Seats = license.Seats,
                MaxCollections = license.MaxCollections,
                MaxStorageGb = _globalSettings.SelfHosted ? 10240 : license.MaxStorageGb, // 10 TB
                UsePolicies = license.UsePolicies,
                UseSso = license.UseSso,
                UseKeyConnector = license.UseKeyConnector,
                UseGroups = license.UseGroups,
                UseDirectory = license.UseDirectory,
                UseEvents = license.UseEvents,
                UseTotp = license.UseTotp,
                Use2fa = license.Use2fa,
                UseApi = license.UseApi,
                UseResetPassword = license.UseResetPassword,
                Plan = license.Plan,
                SelfHost = license.SelfHost,
                UsersGetPremium = license.UsersGetPremium,
                Gateway = null,
                GatewayCustomerId = null,
                GatewaySubscriptionId = null,
                ReferenceData = owner.ReferenceData,
                Enabled = license.Enabled,
                ExpirationDate = license.Expires,
                LicenseKey = license.LicenseKey,
                ApiKey = CoreHelpers.SecureRandomString(30),
                PublicKey = publicKey,
                PrivateKey = privateKey,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow
            };

            var result = await SignUpAsync(organization, owner.Id, ownerKey, collectionName, false);

            var dir = $"{_globalSettings.LicenseDirectory}/organization";
            Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText($"{dir}/{organization.Id}.json",
                JsonConvert.SerializeObject(license, Formatting.Indented));
            return result;
        }

        private async Task<Tuple<Organization, OrganizationUser>> SignUpAsync(Organization organization,
            Guid ownerId, string ownerKey, string collectionName, bool withPayment)
        {
            try
            {
                await _organizationRepository.CreateAsync(organization);
                await _applicationCacheService.UpsertOrganizationAbilityAsync(organization);

                if (!string.IsNullOrWhiteSpace(collectionName))
                {
                    var defaultCollection = new Collection
                    {
                        Name = collectionName,
                        OrganizationId = organization.Id,
                        CreationDate = organization.CreationDate,
                        RevisionDate = organization.CreationDate
                    };
                    await _collectionRepository.CreateAsync(defaultCollection);
                }

                OrganizationUser orgUser = null;
                if (ownerId != default)
                {
                    orgUser = new OrganizationUser
                    {
                        OrganizationId = organization.Id,
                        UserId = ownerId,
                        Key = ownerKey,
                        Type = OrganizationUserType.Owner,
                        Status = OrganizationUserStatusType.Confirmed,
                        AccessAll = true,
                        CreationDate = organization.CreationDate,
                        RevisionDate = organization.CreationDate
                    };

                    await _organizationUserRepository.CreateAsync(orgUser);

                    var deviceIds = await GetUserDeviceIdsAsync(orgUser.UserId.Value);
                    await _pushRegistrationService.AddUserRegistrationOrganizationAsync(deviceIds,
                        organization.Id.ToString());
                    await _pushNotificationService.PushSyncOrgKeysAsync(ownerId);
                }

                return new Tuple<Organization, OrganizationUser>(organization, orgUser);
            }
            catch
            {
                if (withPayment)
                {
                    await _paymentService.CancelAndRecoverChargesAsync(organization);
                }

                if (organization.Id != default(Guid))
                {
                    await _organizationRepository.DeleteAsync(organization);
                    await _applicationCacheService.DeleteOrganizationAbilityAsync(organization.Id);
                }

                throw;
            }
        }

        public async Task UpdateLicenseAsync(Guid organizationId, OrganizationLicense license)
        {
            if (!_globalSettings.SelfHosted)
            {
                throw new InvalidOperationException("Licenses require self hosting.");
            }

            var organization = await GetOrgById(organizationId);
            CoreHelpers.HandlePermissionResult(await _organizationAccessPolicies.CanUpdateLicenseAsync(organization, license));

            var dir = $"{_globalSettings.LicenseDirectory}/organization";
            Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText($"{dir}/{organization.Id}.json",
                JsonConvert.SerializeObject(license, Formatting.Indented));

            organization.Name = license.Name;
            organization.BusinessName = license.BusinessName;
            organization.BillingEmail = license.BillingEmail;
            organization.PlanType = license.PlanType;
            organization.Seats = license.Seats;
            organization.MaxCollections = license.MaxCollections;
            organization.UseGroups = license.UseGroups;
            organization.UseDirectory = license.UseDirectory;
            organization.UseEvents = license.UseEvents;
            organization.UseTotp = license.UseTotp;
            organization.Use2fa = license.Use2fa;
            organization.UseApi = license.UseApi;
            organization.UsePolicies = license.UsePolicies;
            organization.UseSso = license.UseSso;
            organization.UseKeyConnector = license.UseKeyConnector;
            organization.UseResetPassword = license.UseResetPassword;
            organization.SelfHost = license.SelfHost;
            organization.UsersGetPremium = license.UsersGetPremium;
            organization.Plan = license.Plan;
            organization.Enabled = license.Enabled;
            organization.ExpirationDate = license.Expires;
            organization.LicenseKey = license.LicenseKey;
            organization.RevisionDate = DateTime.UtcNow;
            await ReplaceAndUpdateCache(organization);
        }

        public async Task DeleteAsync(Organization organization)
        {
            await ValidateDeleteOrganizationAsync(organization);

            if (!string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
            {
                try
                {
                    var eop = !organization.ExpirationDate.HasValue ||
                        organization.ExpirationDate.Value >= DateTime.UtcNow;
                    await _paymentService.CancelSubscriptionAsync(organization, eop);
                    await _referenceEventService.RaiseEventAsync(
                        new ReferenceEvent(ReferenceEventType.DeleteAccount, organization));
                }
                catch (GatewayException) { }
            }

            await _organizationRepository.DeleteAsync(organization);
            await _applicationCacheService.DeleteOrganizationAbilityAsync(organization.Id);
        }

        public async Task EnableAsync(Guid organizationId, DateTime? expirationDate)
        {
            var org = await GetOrgById(organizationId);
            if (org != null && !org.Enabled && org.Gateway.HasValue)
            {
                org.Enabled = true;
                org.ExpirationDate = expirationDate;
                org.RevisionDate = DateTime.UtcNow;
                await ReplaceAndUpdateCache(org);
            }
        }

        public async Task DisableAsync(Guid organizationId, DateTime? expirationDate)
        {
            var org = await GetOrgById(organizationId);
            if (org != null && org.Enabled)
            {
                org.Enabled = false;
                org.ExpirationDate = expirationDate;
                org.RevisionDate = DateTime.UtcNow;
                await ReplaceAndUpdateCache(org);

                // TODO: send email to owners?
            }
        }

        public async Task UpdateExpirationDateAsync(Guid organizationId, DateTime? expirationDate)
        {
            var org = await GetOrgById(organizationId);
            if (org != null)
            {
                org.ExpirationDate = expirationDate;
                org.RevisionDate = DateTime.UtcNow;
                await ReplaceAndUpdateCache(org);
            }
        }

        public async Task UpdateAsync(Organization organization, bool updateBilling = false)
        {
            if (organization.Id == default(Guid))
            {
                throw new ApplicationException("Cannot create org this way. Call SignUpAsync.");
            }

            if (!string.IsNullOrWhiteSpace(organization.Identifier))
            {
                var orgById = await _organizationRepository.GetByIdentifierAsync(organization.Identifier);
                if (orgById != null && orgById.Id != organization.Id)
                {
                    throw new BadRequestException("Identifier already in use by another organization.");
                }
            }

            await ReplaceAndUpdateCache(organization, EventType.Organization_Updated);

            if (updateBilling && !string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
            {
                var customerService = new CustomerService();
                await customerService.UpdateAsync(organization.GatewayCustomerId, new CustomerUpdateOptions
                {
                    Email = organization.BillingEmail,
                    Description = organization.BusinessName
                });
            }
        }

        public async Task UpdateTwoFactorProviderAsync(Organization organization, TwoFactorProviderType type)
        {
            if (!type.ToString().Contains("Organization"))
            {
                throw new ArgumentException("Not an organization provider type.");
            }

            if (!organization.Use2fa)
            {
                throw new BadRequestException("Organization cannot use 2FA.");
            }

            var providers = organization.GetTwoFactorProviders();
            if (!providers?.ContainsKey(type) ?? true)
            {
                return;
            }

            providers[type].Enabled = true;
            organization.SetTwoFactorProviders(providers);
            await UpdateAsync(organization);
        }

        public async Task DisableTwoFactorProviderAsync(Organization organization, TwoFactorProviderType type)
        {
            if (!type.ToString().Contains("Organization"))
            {
                throw new ArgumentException("Not an organization provider type.");
            }

            var providers = organization.GetTwoFactorProviders();
            if (!providers?.ContainsKey(type) ?? true)
            {
                return;
            }

            providers.Remove(type);
            organization.SetTwoFactorProviders(providers);
            await UpdateAsync(organization);
        }

        // TODO Remove when UserService is split up in to features.
        public async Task<OrganizationUser> AcceptUserAsync(string orgIdentifier, User user, IUserService userService)
        {
            var org = await _organizationRepository.GetByIdentifierAsync(orgIdentifier);
            if (org == null)
            {
                throw new BadRequestException("Organization invalid.");
            }

            var usersOrgs = await _organizationUserRepository.GetManyByUserAsync(user.Id);
            var orgUser = usersOrgs.FirstOrDefault(u => u.OrganizationId == org.Id);
            if (orgUser == null)
            {
                throw new BadRequestException("User not found within organization.");
            }

            if (orgUser.Status != OrganizationUserStatusType.Invited)
            {
                throw new BadRequestException("Already accepted.");
            }

            if (orgUser.Type == OrganizationUserType.Owner || orgUser.Type == OrganizationUserType.Admin)
            {
                if (org.PlanType == PlanType.Free)
                {
                    var adminCount = await _organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(
                        user.Id);
                    if (adminCount > 0)
                    {
                        throw new BadRequestException("You can only be an admin of one free organization.");
                    }
                }
            }

            // Enforce Single Organization Policy of organization user is trying to join
            var allOrgUsers = await _organizationUserRepository.GetManyByUserAsync(user.Id);
            var hasOtherOrgs = allOrgUsers.Any(ou => ou.OrganizationId != orgUser.OrganizationId);
            var invitedSingleOrgPolicies = await _policyRepository.GetManyByTypeApplicableToUserIdAsync(user.Id,
                PolicyType.SingleOrg, OrganizationUserStatusType.Invited);

            if (hasOtherOrgs && invitedSingleOrgPolicies.Any(p => p.OrganizationId == orgUser.OrganizationId))
            {
                throw new BadRequestException("You may not join this organization until you leave or remove " +
                    "all other organizations.");
            }

            // Enforce Single Organization Policy of other organizations user is a member of
            var singleOrgPolicyCount = await _policyRepository.GetCountByTypeApplicableToUserIdAsync(user.Id,
                PolicyType.SingleOrg);
            if (singleOrgPolicyCount > 0)
            {
                throw new BadRequestException("You cannot join this organization because you are a member of " +
                    "another organization which forbids it");
            }

            // Enforce Two Factor Authentication Policy of organization user is trying to join
            if (!await userService.TwoFactorIsEnabledAsync(user))
            {
                var invitedTwoFactorPolicies = await _policyRepository.GetManyByTypeApplicableToUserIdAsync(user.Id,
                    PolicyType.TwoFactorAuthentication, OrganizationUserStatusType.Invited);
                if (invitedTwoFactorPolicies.Any(p => p.OrganizationId == orgUser.OrganizationId))
                {
                    throw new BadRequestException("You cannot join this organization until you enable " +
                        "two-step login on your user account.");
                }
            }

            orgUser.Status = OrganizationUserStatusType.Accepted;
            orgUser.UserId = user.Id;
            orgUser.Email = null;

            await _organizationUserRepository.ReplaceAsync(orgUser);

            await _organizationUserMailer.SendOrganizationAcceptedEmailAsync(org, user);
            return orgUser;
        }


        // TODO Remove when UserService is split up in to features.
        public async Task DeleteUserAsync(Guid organizationId, Guid userId)
        {
            var orgUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId, userId);
            if (orgUser == null)
            {
                throw new NotFoundException();
            }

            if (!await HasConfirmedOwnersExceptAsync(organizationId, new[] { orgUser.Id }))
            {
                throw new BadRequestException("Organization must have at least one confirmed owner.");
            }

            await _organizationUserRepository.DeleteAsync(orgUser);
            await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Removed);

            if (orgUser.UserId.HasValue)
            {
                await DeleteAndPushUserRegistrationAsync(organizationId, orgUser.UserId.Value);
            }
        }

        public async Task<bool> HasConfirmedOwnersExceptAsync(Guid organizationId, IEnumerable<Guid> organizationUserIds, bool includeProvider = true)
        {
            var confirmedOwners = await GetConfirmedOwnersAsync(organizationId);
            var confirmedOwnersIds = confirmedOwners.Select(u => u.Id);
            bool hasOtherOwner = confirmedOwnersIds.Except(organizationUserIds).Any();
            if (!hasOtherOwner && includeProvider)
            {
                return (await _currentContext.ProviderIdForOrg(organizationId)).HasValue;
            }
            return hasOtherOwner;
        }

        public async Task<OrganizationLicense> GenerateLicenseAsync(Guid organizationId, Guid installationId)
        {
            var organization = await GetOrgById(organizationId);
            return await GenerateLicenseAsync(organization, installationId);
        }

        public async Task<OrganizationLicense> GenerateLicenseAsync(Organization organization, Guid installationId,
            int? version = null)
        {
            if (organization == null)
            {
                throw new NotFoundException();
            }

            var installation = await _installationRepository.GetByIdAsync(installationId);
            if (installation == null || !installation.Enabled)
            {
                throw new BadRequestException("Invalid installation id");
            }

            var subInfo = await _paymentService.GetSubscriptionAsync(organization);
            return new OrganizationLicense(organization, subInfo, installationId, _licensingService, version);
        }

        public async Task RotateApiKeyAsync(Organization organization)
        {
            organization.ApiKey = CoreHelpers.SecureRandomString(30);
            organization.RevisionDate = DateTime.UtcNow;
            await ReplaceAndUpdateCache(organization);
        }

        // TODO: this feels like it doesn't belong. Make SsoUserService?
        public async Task DeleteSsoUserAsync(Guid userId, Guid? organizationId)
        {
            await _ssoUserRepository.DeleteAsync(userId, organizationId);
            if (organizationId.HasValue)
            {
                var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId.Value, userId);
                if (organizationUser != null)
                {
                    await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_UnlinkedSso);
                }
            }
        }

        public async Task<Organization> UpdateOrganizationKeysAsync(Guid orgId, string publicKey, string privateKey)
        {
            if (!await _currentContext.ManageResetPassword(orgId))
            {
                throw new UnauthorizedAccessException();
            }

            // If the keys already exist, error out
            var org = await _organizationRepository.GetByIdAsync(orgId);
            if (org.PublicKey != null && org.PrivateKey != null)
            {
                throw new BadRequestException("Organization Keys already exist");
            }

            // Update org with generated public/private key
            org.PublicKey = publicKey;
            org.PrivateKey = privateKey;
            await UpdateAsync(org);

            return org;
        }

        private async Task<IEnumerable<OrganizationUser>> GetConfirmedOwnersAsync(Guid organizationId)
        {
            var owners = await _organizationUserRepository.GetManyByOrganizationAsync(organizationId,
                OrganizationUserType.Owner);
            return owners.Where(o => o.Status == OrganizationUserStatusType.Confirmed);
        }

        private async Task DeleteAndPushUserRegistrationAsync(Guid organizationId, Guid userId)
        {
            var deviceIds = await GetUserDeviceIdsAsync(userId);
            await _pushRegistrationService.DeleteUserRegistrationOrganizationAsync(deviceIds,
                organizationId.ToString());
            await _pushNotificationService.PushSyncOrgKeysAsync(userId);
        }


        private async Task<IEnumerable<string>> GetUserDeviceIdsAsync(Guid userId)
        {
            var devices = await _deviceRepository.GetManyByUserIdAsync(userId);
            return devices.Where(d => !string.IsNullOrWhiteSpace(d.PushToken)).Select(d => d.Id.ToString());
        }

        public async Task ReplaceAndUpdateCache(Organization org, EventType? orgEvent = null)
        {
            await _organizationRepository.ReplaceAsync(org);
            await _applicationCacheService.UpsertOrganizationAbilityAsync(org);

            if (orgEvent.HasValue)
            {
                await _eventService.LogOrganizationEventAsync(org, orgEvent.Value);
            }
        }

        private async Task<Organization> GetOrgById(Guid id)
        {
            return await _organizationRepository.GetByIdAsync(id);
        }


        private async Task ValidateDeleteOrganizationAsync(Organization organization)
        {
            var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
            if (ssoConfig?.GetData()?.KeyConnectorEnabled == true)
            {
                throw new BadRequestException("You cannot delete an Organization that is using Key Connector.");
            }
        }
    }
}
