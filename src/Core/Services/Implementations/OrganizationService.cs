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
using System.Text.Json;
using Bit.Core.Models.Api;

namespace Bit.Core.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ICollectionRepository _collectionRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IDataProtector _dataProtector;
        private readonly IMailService _mailService;
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
        private readonly GlobalSettings _globalSettings;
        private readonly ITaxRateRepository _taxRateRepository;

        public OrganizationService(
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
            GlobalSettings globalSettings,
            ITaxRateRepository taxRateRepository)
        {
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _collectionRepository = collectionRepository;
            _userRepository = userRepository;
            _groupRepository = groupRepository;
            _dataProtector = dataProtectionProvider.CreateProtector("OrganizationServiceDataProtector");
            _mailService = mailService;
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
            _taxRateRepository = taxRateRepository;
        }

        public async Task ReplacePaymentMethodAsync(Guid organizationId, string paymentToken,
            PaymentMethodType paymentMethodType, TaxInfo taxInfo)
        {
            var organization = await GetOrgById(organizationId);
            if (organization == null)
            {
                throw new NotFoundException();
            }

            await _paymentService.SaveTaxInfoAsync(organization, taxInfo);
            var updated = await _paymentService.UpdatePaymentMethodAsync(organization,
                paymentMethodType, paymentToken);
            if (updated)
            {
                await ReplaceAndUpdateCache(organization);
            }
        }

        public async Task CancelSubscriptionAsync(Guid organizationId, bool? endOfPeriod = null)
        {
            var organization = await GetOrgById(organizationId);
            if (organization == null)
            {
                throw new NotFoundException();
            }

            var eop = endOfPeriod.GetValueOrDefault(true);
            if (!endOfPeriod.HasValue && organization.ExpirationDate.HasValue &&
                organization.ExpirationDate.Value < DateTime.UtcNow)
            {
                eop = false;
            }

            await _paymentService.CancelSubscriptionAsync(organization, eop);
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.CancelSubscription, organization)
                {
                    EndOfPeriod = endOfPeriod,
                });
        }

        public async Task ReinstateSubscriptionAsync(Guid organizationId)
        {
            var organization = await GetOrgById(organizationId);
            if (organization == null)
            {
                throw new NotFoundException();
            }

            await _paymentService.ReinstateSubscriptionAsync(organization);
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.ReinstateSubscription, organization));
        }

        public async Task<Tuple<bool, string>> UpgradePlanAsync(Guid organizationId, OrganizationUpgrade upgrade)
        {
            var organization = await GetOrgById(organizationId);
            if (organization == null)
            {
                throw new NotFoundException();
            }

            if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
            {
                throw new BadRequestException("Your account has no payment method available.");
            }

            var existingPlan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType);
            if (existingPlan == null)
            {
                throw new BadRequestException("Existing plan not found.");
            }

            var newPlan = StaticStore.Plans.FirstOrDefault(p => p.Type == upgrade.Plan && !p.Disabled);
            if (newPlan == null)
            {
                throw new BadRequestException("Plan not found.");
            }

            if (existingPlan.Type == newPlan.Type)
            {
                throw new BadRequestException("Organization is already on this plan.");
            }

            if (existingPlan.UpgradeSortOrder >= newPlan.UpgradeSortOrder)
            {
                throw new BadRequestException("You cannot upgrade to this plan.");
            }

            if (existingPlan.Type != PlanType.Free)
            {
                throw new BadRequestException("You can only upgrade from the free plan. Contact support.");
            }

            ValidateOrganizationUpgradeParameters(newPlan, upgrade);

            var newPlanSeats = (short)(newPlan.BaseSeats +
                (newPlan.HasAdditionalSeatsOption ? upgrade.AdditionalSeats : 0));
            if (!organization.Seats.HasValue || organization.Seats.Value > newPlanSeats)
            {
                var userCount = await _organizationUserRepository.GetCountByOrganizationIdAsync(organization.Id);
                if (userCount > newPlanSeats)
                {
                    throw new BadRequestException($"Your organization currently has {userCount} seats filled. " +
                        $"Your new plan only has ({newPlanSeats}) seats. Remove some users.");
                }
            }

            if (newPlan.MaxCollections.HasValue && (!organization.MaxCollections.HasValue ||
                organization.MaxCollections.Value > newPlan.MaxCollections.Value))
            {
                var collectionCount = await _collectionRepository.GetCountByOrganizationIdAsync(organization.Id);
                if (collectionCount > newPlan.MaxCollections.Value)
                {
                    throw new BadRequestException($"Your organization currently has {collectionCount} collections. " +
                        $"Your new plan allows for a maximum of ({newPlan.MaxCollections.Value}) collections. " +
                        "Remove some collections.");
                }
            }

            if (!newPlan.HasGroups && organization.UseGroups)
            {
                var groups = await _groupRepository.GetManyByOrganizationIdAsync(organization.Id);
                if (groups.Any())
                {
                    throw new BadRequestException($"Your new plan does not allow the groups feature. " +
                        $"Remove your groups.");
                }
            }

            if (!newPlan.HasPolicies && organization.UsePolicies)
            {
                var policies = await _policyRepository.GetManyByOrganizationIdAsync(organization.Id);
                if (policies.Any(p => p.Enabled))
                {
                    throw new BadRequestException($"Your new plan does not allow the policies feature. " +
                        $"Disable your policies.");
                }
            }

            if (!newPlan.HasSso && organization.UseSso)
            {
                var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
                if (ssoConfig != null && ssoConfig.Enabled)
                {
                    throw new BadRequestException($"Your new plan does not allow the SSO feature. " +
                        $"Disable your SSO configuration.");
                }
            }
            
            if (!newPlan.HasResetPassword && organization.UseResetPassword)
            {
                var resetPasswordPolicy =
                    await _policyRepository.GetByOrganizationIdTypeAsync(organization.Id, PolicyType.ResetPassword);
                if (resetPasswordPolicy != null && resetPasswordPolicy.Enabled)
                {
                    throw new BadRequestException("Your new plan does not allow the Password Reset feature. " + 
                        "Disable your Password Reset policy.");
                }
            }

            // TODO: Check storage?

            string paymentIntentClientSecret = null;
            var success = true;
            if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
            {
                paymentIntentClientSecret = await _paymentService.UpgradeFreeOrganizationAsync(organization, newPlan,
                    upgrade.AdditionalStorageGb, upgrade.AdditionalSeats, upgrade.PremiumAccessAddon, upgrade.TaxInfo);
                success = string.IsNullOrWhiteSpace(paymentIntentClientSecret);
            }
            else
            {
                // TODO: Update existing sub
                throw new BadRequestException("You can only upgrade from the free plan. Contact support.");
            }

            organization.BusinessName = upgrade.BusinessName;
            organization.PlanType = newPlan.Type;
            organization.Seats = (short)(newPlan.BaseSeats + upgrade.AdditionalSeats);
            organization.MaxCollections = newPlan.MaxCollections;
            organization.UseGroups = newPlan.HasGroups;
            organization.UseDirectory = newPlan.HasDirectory;
            organization.UseEvents = newPlan.HasEvents;
            organization.UseTotp = newPlan.HasTotp;
            organization.Use2fa = newPlan.Has2fa;
            organization.UseApi = newPlan.HasApi;
            organization.SelfHost = newPlan.HasSelfHost;
            organization.UsePolicies = newPlan.HasPolicies;
            organization.MaxStorageGb = !newPlan.BaseStorageGb.HasValue ?
                (short?)null : (short)(newPlan.BaseStorageGb.Value + upgrade.AdditionalStorageGb);
            organization.UseGroups = newPlan.HasGroups;
            organization.UseDirectory = newPlan.HasDirectory;
            organization.UseEvents = newPlan.HasEvents;
            organization.UseTotp = newPlan.HasTotp;
            organization.Use2fa = newPlan.Has2fa;
            organization.UseApi = newPlan.HasApi;
            organization.UseSso = newPlan.HasSso;
            organization.UseResetPassword = newPlan.HasResetPassword;
            organization.SelfHost = newPlan.HasSelfHost;
            organization.UsersGetPremium = newPlan.UsersGetPremium || upgrade.PremiumAccessAddon;
            organization.Plan = newPlan.Name;
            organization.Enabled = success;
            organization.PublicKey = upgrade.PublicKey;
            organization.PrivateKey = upgrade.PrivateKey;
            await ReplaceAndUpdateCache(organization);
            if (success)
            {
                await _referenceEventService.RaiseEventAsync(
                    new ReferenceEvent(ReferenceEventType.UpgradePlan, organization)
                    {
                        PlanName = newPlan.Name,
                        PlanType = newPlan.Type,
                        Seats = organization.Seats,
                        Storage = organization.MaxStorageGb,
                    });
            }

            return new Tuple<bool, string>(success, paymentIntentClientSecret);
        }

        public async Task<string> AdjustStorageAsync(Guid organizationId, short storageAdjustmentGb)
        {
            var organization = await GetOrgById(organizationId);
            if (organization == null)
            {
                throw new NotFoundException();
            }

            var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType);
            if (plan == null)
            {
                throw new BadRequestException("Existing plan not found.");
            }

            if (!plan.HasAdditionalStorageOption)
            {
                throw new BadRequestException("Plan does not allow additional storage.");
            }

            var secret = await BillingHelpers.AdjustStorageAsync(_paymentService, organization, storageAdjustmentGb,
                plan.StripeStoragePlanId);
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.AdjustStorage, organization)
                {
                    PlanName = plan.Name,
                    PlanType = plan.Type,
                    Storage = storageAdjustmentGb,
                });
            await ReplaceAndUpdateCache(organization);
            return secret;
        }

        public async Task<string> AdjustSeatsAsync(Guid organizationId, int seatAdjustment)
        {
            var organization = await GetOrgById(organizationId);
            if (organization == null)
            {
                throw new NotFoundException();
            }

            if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
            {
                throw new BadRequestException("No payment method found.");
            }

            if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
            {
                throw new BadRequestException("No subscription found.");
            }

            var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType);
            if (plan == null)
            {
                throw new BadRequestException("Existing plan not found.");
            }

            if (!plan.HasAdditionalSeatsOption)
            {
                throw new BadRequestException("Plan does not allow additional seats.");
            }

            var newSeatTotal = organization.Seats + seatAdjustment;
            if (plan.BaseSeats > newSeatTotal)
            {
                throw new BadRequestException($"Plan has a minimum of {plan.BaseSeats} seats.");
            }

            if (newSeatTotal <= 0)
            {
                throw new BadRequestException("You must have at least 1 seat.");
            }

            var additionalSeats = newSeatTotal - plan.BaseSeats;
            if (plan.MaxAdditionalSeats.HasValue && additionalSeats > plan.MaxAdditionalSeats.Value)
            {
                throw new BadRequestException($"Organization plan allows a maximum of " +
                    $"{plan.MaxAdditionalSeats.Value} additional seats.");
            }

            if (!organization.Seats.HasValue || organization.Seats.Value > newSeatTotal)
            {
                var userCount = await _organizationUserRepository.GetCountByOrganizationIdAsync(organization.Id);
                if (userCount > newSeatTotal)
                {
                    throw new BadRequestException($"Your organization currently has {userCount} seats filled. " +
                        $"Your new plan only has ({newSeatTotal}) seats. Remove some users.");
                }
            }

            var subscriptionItemService = new SubscriptionItemService();
            var subscriptionService = new SubscriptionService();
            var sub = await subscriptionService.GetAsync(organization.GatewaySubscriptionId);
            if (sub == null)
            {
                throw new BadRequestException("Subscription not found.");
            }

            var prorationDate = DateTime.UtcNow;
            var seatItem = sub.Items?.Data?.FirstOrDefault(i => i.Plan.Id == plan.StripeSeatPlanId);
            // Retain original collection method
            var collectionMethod = sub.CollectionMethod;

            var subUpdateOptions = new SubscriptionUpdateOptions
            {
                Items = new List<SubscriptionItemOptions>
                {
                    new SubscriptionItemOptions
                    {
                        Id = seatItem?.Id,
                        Plan = plan.StripeSeatPlanId,
                        Quantity = additionalSeats,
                        Deleted = (seatItem?.Id != null && additionalSeats == 0) ? true : (bool?)null
                    }
                },
                ProrationBehavior = "always_invoice",
                DaysUntilDue = 1,
                CollectionMethod = "send_invoice",
                ProrationDate = prorationDate,
            };

            var customer = await new CustomerService().GetAsync(sub.CustomerId);
            if (!string.IsNullOrWhiteSpace(customer?.Address?.Country)
                    && !string.IsNullOrWhiteSpace(customer?.Address?.PostalCode))
            {
                var taxRates = await _taxRateRepository.GetByLocationAsync(
                    new Bit.Core.Models.Table.TaxRate()
                    {
                        Country = customer.Address.Country,
                        PostalCode = customer.Address.PostalCode
                    }
                );
                var taxRate = taxRates.FirstOrDefault();
                if (taxRate != null && !sub.DefaultTaxRates.Any(x => x.Equals(taxRate.Id)))
                {
                    subUpdateOptions.DefaultTaxRates = new List<string>(1)
                    {
                        taxRate.Id
                    };
                }
            }

            var subResponse = await subscriptionService.UpdateAsync(sub.Id, subUpdateOptions);

            string paymentIntentClientSecret = null;
            if (additionalSeats > 0)
            {
                try
                {
                    paymentIntentClientSecret = await (_paymentService as StripePaymentService)
                        .PayInvoiceAfterSubscriptionChangeAsync(organization, subResponse.LatestInvoiceId);
                }
                catch
                {
                    // Need to revert the subscription
                    await subscriptionService.UpdateAsync(sub.Id, new SubscriptionUpdateOptions
                    {
                        Items = new List<SubscriptionItemOptions>
                        {
                            new SubscriptionItemOptions
                            {
                                Id = seatItem?.Id,
                                Plan = plan.StripeSeatPlanId,
                                Quantity = organization.Seats,
                                Deleted = seatItem?.Id == null ? true : (bool?)null
                            }
                        },
                        // This proration behavior prevents a false "credit" from
                        //  being applied forward to the next month's invoice
                        ProrationBehavior = "none",
                        CollectionMethod = collectionMethod,
                    });
                    throw;
                }
            }

            // Change back the subscription collection method
            if (collectionMethod != "send_invoice")
            {
                await subscriptionService.UpdateAsync(sub.Id, new SubscriptionUpdateOptions
                {
                    CollectionMethod = collectionMethod,
                });
            }

            organization.Seats = (short?)newSeatTotal;
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.AdjustSeats, organization)
                {
                    PlanName = plan.Name,
                    PlanType = plan.Type,
                    Seats = organization.Seats,
                });
            await ReplaceAndUpdateCache(organization);
            return paymentIntentClientSecret;
        }

        public async Task VerifyBankAsync(Guid organizationId, int amount1, int amount2)
        {
            var organization = await GetOrgById(organizationId);
            if (organization == null)
            {
                throw new NotFoundException();
            }

            if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
            {
                throw new GatewayException("Not a gateway customer.");
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

        public async Task<Tuple<Organization, OrganizationUser>> SignUpAsync(OrganizationSignup signup)
        {
            var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == signup.Plan && !p.Disabled);
            if (plan == null)
            {
                throw new BadRequestException("Plan not found.");
            }

            ValidateOrganizationUpgradeParameters(plan, signup);

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

            if (plan.Type == PlanType.Free)
            {
                var adminCount =
                    await _organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(signup.Owner.Id);
                if (adminCount > 0)
                {
                    throw new BadRequestException("You can only be an admin of one free organization.");
                }
            }
            else
            {
                await _paymentService.PurchaseOrganizationAsync(organization, signup.PaymentMethodType.Value,
                    signup.PaymentToken, plan, signup.AdditionalStorageGb, signup.AdditionalSeats,
                    signup.PremiumAccessAddon, signup.TaxInfo);
            }

            var returnValue = await SignUpAsync(organization, signup.Owner.Id, signup.OwnerKey, signup.CollectionName, true);
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

        public async Task<Tuple<Organization, OrganizationUser>> SignUpAsync(
            OrganizationLicense license, User owner, string ownerKey, string collectionName, string publicKey,
            string privateKey)
        {
            if (license == null || !_licensingService.VerifyLicense(license))
            {
                throw new BadRequestException("Invalid license.");
            }

            if (!license.CanUse(_globalSettings))
            {
                throw new BadRequestException("Invalid license. Make sure your license allows for on-premise " +
                    "hosting of organizations and that the installation id matches your current installation.");
            }

            if (license.PlanType != PlanType.Custom &&
                StaticStore.Plans.FirstOrDefault(p => p.Type == license.PlanType && !p.Disabled) == null)
            {
                throw new BadRequestException("Plan not found.");
            }

            var enabledOrgs = await _organizationRepository.GetManyByEnabledAsync();
            if (enabledOrgs.Any(o => o.LicenseKey.Equals(license.LicenseKey)))
            {
                throw new BadRequestException("License is already in use by another organization.");
            }

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

                var orgUser = new OrganizationUser
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

                // push
                var deviceIds = await GetUserDeviceIdsAsync(orgUser.UserId.Value);
                await _pushRegistrationService.AddUserRegistrationOrganizationAsync(deviceIds,
                    organization.Id.ToString());
                await _pushNotificationService.PushSyncOrgKeysAsync(ownerId);

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
            var organization = await GetOrgById(organizationId);
            if (organization == null)
            {
                throw new NotFoundException();
            }

            if (!_globalSettings.SelfHosted)
            {
                throw new InvalidOperationException("Licenses require self hosting.");
            }

            if (license == null || !_licensingService.VerifyLicense(license))
            {
                throw new BadRequestException("Invalid license.");
            }

            if (!license.CanUse(_globalSettings))
            {
                throw new BadRequestException("Invalid license. Make sure your license allows for on-premise " +
                    "hosting of organizations and that the installation id matches your current installation.");
            }

            var enabledOrgs = await _organizationRepository.GetManyByEnabledAsync();
            if (enabledOrgs.Any(o => o.LicenseKey.Equals(license.LicenseKey) && o.Id != organizationId))
            {
                throw new BadRequestException("License is already in use by another organization.");
            }

            if (license.Seats.HasValue &&
                (!organization.Seats.HasValue || organization.Seats.Value > license.Seats.Value))
            {
                var userCount = await _organizationUserRepository.GetCountByOrganizationIdAsync(organization.Id);
                if (userCount > license.Seats.Value)
                {
                    throw new BadRequestException($"Your organization currently has {userCount} seats filled. " +
                        $"Your new license only has ({ license.Seats.Value}) seats. Remove some users.");
                }
            }

            if (license.MaxCollections.HasValue && (!organization.MaxCollections.HasValue ||
                organization.MaxCollections.Value > license.MaxCollections.Value))
            {
                var collectionCount = await _collectionRepository.GetCountByOrganizationIdAsync(organization.Id);
                if (collectionCount > license.MaxCollections.Value)
                {
                    throw new BadRequestException($"Your organization currently has {collectionCount} collections. " +
                        $"Your new license allows for a maximum of ({license.MaxCollections.Value}) collections. " +
                        "Remove some collections.");
                }
            }

            if (!license.UseGroups && organization.UseGroups)
            {
                var groups = await _groupRepository.GetManyByOrganizationIdAsync(organization.Id);
                if (groups.Count > 0)
                {
                    throw new BadRequestException($"Your organization currently has {groups.Count} groups. " +
                        $"Your new license does not allow for the use of groups. Remove all groups.");
                }
            }

            if (!license.UsePolicies && organization.UsePolicies)
            {
                var policies = await _policyRepository.GetManyByOrganizationIdAsync(organization.Id);
                if (policies.Any(p => p.Enabled))
                {
                    throw new BadRequestException($"Your organization currently has {policies.Count} enabled " +
                        $"policies. Your new license does not allow for the use of policies. Disable all policies.");
                }
            }

            if (!license.UseSso && organization.UseSso)
            {
                var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
                if (ssoConfig != null && ssoConfig.Enabled)
                {
                    throw new BadRequestException($"Your organization currently has a SSO configuration. " +
                        $"Your new license does not allow for the use of SSO. Disable your SSO configuration.");
                }
            }
            
            if (!license.UseResetPassword && organization.UseResetPassword)
            {
                var resetPasswordPolicy =
                    await _policyRepository.GetByOrganizationIdTypeAsync(organization.Id, PolicyType.ResetPassword);
                if (resetPasswordPolicy != null && resetPasswordPolicy.Enabled)
                {
                    throw new BadRequestException("Your new license does not allow the Password Reset feature. " 
                        + "Disable your Password Reset policy.");
                }
            }

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

        public async Task EnableAsync(Guid organizationId)
        {
            var org = await GetOrgById(organizationId);
            if (org != null && !org.Enabled)
            {
                org.Enabled = true;
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

        private async Task<List<OrganizationUser>> InviteUsersAsync(Guid organizationId, Guid? invitingUserId,
            IEnumerable<(OrganizationUserInvite invite, string externalId)> invites)
        {
            var organization = await GetOrgById(organizationId);
            if (organization == null || invites.Any(i => i.invite.Emails == null || i.externalId == null))
            {
                throw new NotFoundException();
            }

            var inviteTypes = new HashSet<OrganizationUserType>(invites.Where(i => i.invite.Type.HasValue)
                .Select(i => i.invite.Type.Value));
            if (invitingUserId.HasValue && inviteTypes.Count > 0)
            {
                foreach (var type in inviteTypes)
                {
                    await ValidateOrganizationUserUpdatePermissionsAsync(invitingUserId.Value, organizationId, type, null);
                }
            }

            if (organization.Seats.HasValue)
            {
                var userCount = await _organizationUserRepository.GetCountByOrganizationIdAsync(organizationId);
                var availableSeats = organization.Seats.Value - userCount;
                if (availableSeats < invites.Select(i => i.invite.Emails.Count()).Sum())
                {
                    throw new BadRequestException("You have reached the maximum number of users " +
                        $"({organization.Seats.Value}) for this organization.");
                }
            }

            var orgUsers = new List<OrganizationUser>();
            var orgUserInvitedCount = 0;
            var exceptions = new List<Exception>();
            var events = new List<(OrganizationUser, EventType, DateTime?)>();
            var existingEmails = new HashSet<string>(await _organizationUserRepository.SelectKnownEmailsAsync(
                organizationId, invites.SelectMany(i => i.invite.Emails), false), StringComparer.InvariantCultureIgnoreCase);
            foreach (var (invite, externalId) in invites)
            {
                foreach (var email in invite.Emails)
                {
                    try
                    {
                        // Make sure user is not already invited
                        if (existingEmails.Contains(email))
                        {
                            continue;
                        }

                        var orgUser = new OrganizationUser
                        {
                            OrganizationId = organizationId,
                            UserId = null,
                            Email = email.ToLowerInvariant(),
                            Key = null,
                            Type = invite.Type.Value,
                            Status = OrganizationUserStatusType.Invited,
                            AccessAll = invite.AccessAll,
                            ExternalId = externalId,
                            CreationDate = DateTime.UtcNow,
                            RevisionDate = DateTime.UtcNow,
                        };

                        if (invite.Permissions != null)
                        {
                            orgUser.Permissions = System.Text.Json.JsonSerializer.Serialize(invite.Permissions, new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            });
                        }

                        if (!orgUser.AccessAll && invite.Collections.Any())
                        {
                            throw new Exception("Bulk invite does not support limited collection invites");
                        }

                        events.Add((orgUser, EventType.OrganizationUser_Invited, DateTime.UtcNow));
                        orgUsers.Add(orgUser);
                        orgUserInvitedCount++;
                    }
                    catch (Exception e)
                    {
                        exceptions.Add(e);
                    }
                }
            }

            try
            {
                await _organizationUserRepository.CreateManyAsync(orgUsers);
                await SendInvitesAsync(orgUsers, organization);
                await _eventService.LogOrganizationUserEventsAsync(events);

                await _referenceEventService.RaiseEventAsync(
                    new ReferenceEvent(ReferenceEventType.InvitedUsers, organization)
                    {
                        Users = orgUserInvitedCount
                    });
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }

            if (exceptions.Any())
            {
                throw new AggregateException("One or more errors occurred while inviting users.", exceptions);
            }

            return orgUsers;
        }

        public async Task<List<OrganizationUser>> InviteUserAsync(Guid organizationId, Guid? invitingUserId,
            string externalId, OrganizationUserInvite invite)
        {
            var organization = await GetOrgById(organizationId);
            if (organization == null || invite?.Emails == null)
            {
                throw new NotFoundException();
            }

            if (invitingUserId.HasValue && invite.Type.HasValue)
            {
                await ValidateOrganizationUserUpdatePermissionsAsync(invitingUserId.Value, organizationId, invite.Type.Value, null);
            }

            if (organization.Seats.HasValue)
            {
                var userCount = await _organizationUserRepository.GetCountByOrganizationIdAsync(organizationId);
                var availableSeats = organization.Seats.Value - userCount;
                if (availableSeats < invite.Emails.Count())
                {
                    throw new BadRequestException("You have reached the maximum number of users " +
                        $"({organization.Seats.Value}) for this organization.");
                }
            }

            var orgUsers = new List<OrganizationUser>();
            var orgUserInvitedCount = 0;
            foreach (var email in invite.Emails)
            {
                // Make sure user is not already invited
                var existingOrgUserCount = await _organizationUserRepository.GetCountByOrganizationAsync(
                    organizationId, email, false);
                if (existingOrgUserCount > 0)
                {
                    continue;
                }

                var orgUser = new OrganizationUser
                {
                    OrganizationId = organizationId,
                    UserId = null,
                    Email = email.ToLowerInvariant(),
                    Key = null,
                    Type = invite.Type.Value,
                    Status = OrganizationUserStatusType.Invited,
                    AccessAll = invite.AccessAll,
                    ExternalId = externalId,
                    CreationDate = DateTime.UtcNow,
                    RevisionDate = DateTime.UtcNow,
                };

                if (invite.Permissions != null)
                {
                    orgUser.Permissions = System.Text.Json.JsonSerializer.Serialize(invite.Permissions, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    });
                }

                if (!orgUser.AccessAll && invite.Collections.Any())
                {
                    await _organizationUserRepository.CreateAsync(orgUser, invite.Collections);
                }
                else
                {
                    await _organizationUserRepository.CreateAsync(orgUser);
                }

                await SendInviteAsync(orgUser, organization);
                await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Invited);
                orgUsers.Add(orgUser);
                orgUserInvitedCount++;
            }
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.InvitedUsers, organization)
                {
                    Users = orgUserInvitedCount
                });

            return orgUsers;
        }

        public async Task<IEnumerable<Tuple<OrganizationUser, string>>> ResendInvitesAsync(Guid organizationId, Guid? invitingUserId,
            IEnumerable<Guid> organizationUsersId)
        {
            var orgUsers = await _organizationUserRepository.GetManyAsync(organizationUsersId);
            var org = await GetOrgById(organizationId);

            var result = new List<Tuple<OrganizationUser, string>>();
            foreach (var orgUser in orgUsers)
            {
                if (orgUser.Status != OrganizationUserStatusType.Invited || orgUser.OrganizationId != organizationId)
                {
                    result.Add(Tuple.Create(orgUser, "User invalid."));
                    continue;
                }

                await SendInviteAsync(orgUser, org);
                result.Add(Tuple.Create(orgUser, ""));
            }

            return result;
        }

        public async Task ResendInviteAsync(Guid organizationId, Guid? invitingUserId, Guid organizationUserId)
        {
            var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
            if (orgUser == null || orgUser.OrganizationId != organizationId ||
                orgUser.Status != OrganizationUserStatusType.Invited)
            {
                throw new BadRequestException("User invalid.");
            }

            var org = await GetOrgById(orgUser.OrganizationId);
            await SendInviteAsync(orgUser, org);
        }

        private async Task SendInvitesAsync(IEnumerable<OrganizationUser> orgUsers, Organization organization)
        {
            string MakeToken(OrganizationUser orgUser) =>
                _dataProtector.Protect($"OrganizationUserInvite {orgUser.Id} {orgUser.Email} {CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow)}");
            await _mailService.BulkSendOrganizationInviteEmailAsync(organization.Name,
                orgUsers.Select(o => (o, MakeToken(o))));
        }

        private async Task SendInviteAsync(OrganizationUser orgUser, Organization organization)
        {
            var nowMillis = CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow);
            var token = _dataProtector.Protect(
                $"OrganizationUserInvite {orgUser.Id} {orgUser.Email} {nowMillis}");
            await _mailService.SendOrganizationInviteEmailAsync(organization.Name, orgUser, token);
        }

        public async Task<OrganizationUser> AcceptUserAsync(Guid organizationUserId, User user, string token,
            IUserService userService)
        {
            var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
            if (orgUser == null)
            {
                throw new BadRequestException("User invalid.");
            }

            if (!CoreHelpers.UserInviteTokenIsValid(_dataProtector, token, user.Email, orgUser.Id, _globalSettings))
            {
                throw new BadRequestException("Invalid token.");
            }

            var existingOrgUserCount = await _organizationUserRepository.GetCountByOrganizationAsync(
                orgUser.OrganizationId, user.Email, true);
            if (existingOrgUserCount > 0)
            {
                if (orgUser.Status == OrganizationUserStatusType.Accepted)
                {
                    throw new BadRequestException("Invitation already accepted. You will receive an email when your organization membership is confirmed.");
                }
                throw new BadRequestException("You are already part of this organization.");
            }

            if (string.IsNullOrWhiteSpace(orgUser.Email) ||
                !orgUser.Email.Equals(user.Email, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new BadRequestException("User email does not match invite.");
            }

            return await AcceptUserAsync(orgUser, user, userService);
        }

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

            return await AcceptUserAsync(orgUser, user, userService);
        }

        private async Task<OrganizationUser> AcceptUserAsync(OrganizationUser orgUser, User user,
            IUserService userService)
        {
            if (orgUser.Status != OrganizationUserStatusType.Invited)
            {
                throw new BadRequestException("Already accepted.");
            }

            if (orgUser.Type == OrganizationUserType.Owner || orgUser.Type == OrganizationUserType.Admin)
            {
                var org = await GetOrgById(orgUser.OrganizationId);
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

            bool notExempt(OrganizationUser organizationUser)
            {
                return organizationUser.Type != OrganizationUserType.Owner &&
                        organizationUser.Type != OrganizationUserType.Admin;
            }

            var allOrgUsers = await _organizationUserRepository.GetManyByUserAsync(user.Id);

            // Enforce Single Organization Policy of organization user is trying to join
            var thisSingleOrgPolicy = await _policyRepository.GetByOrganizationIdTypeAsync(orgUser.OrganizationId, PolicyType.SingleOrg);
            if (thisSingleOrgPolicy != null &&
                thisSingleOrgPolicy.Enabled &&
                notExempt(orgUser) &&
                allOrgUsers.Any(ou => ou.OrganizationId != orgUser.OrganizationId))
            {
                throw new BadRequestException("You may not join this organization until you leave or remove " +
                    "all other organizations.");
            }

            // Enforce Single Organization Policy of other organizations user is a member of
            var policies = await _policyRepository.GetManyByUserIdAsync(user.Id);

            var orgsWithSingleOrgPolicy = policies.Where(p => p.Enabled && p.Type == PolicyType.SingleOrg)
                .Select(p => p.OrganizationId);
            var blockedBySingleOrgPolicy = allOrgUsers.Any(ou => notExempt(ou) &&
                ou.Status != OrganizationUserStatusType.Invited &&
                orgsWithSingleOrgPolicy.Contains(ou.OrganizationId));

            if (blockedBySingleOrgPolicy)
            {
                throw new BadRequestException("You cannot join this organization because you are a member of " +
                    "an organization which forbids it");
            }

            var twoFactorPolicy = await _policyRepository.GetByOrganizationIdTypeAsync(orgUser.OrganizationId, PolicyType.TwoFactorAuthentication);
            if (!await userService.TwoFactorIsEnabledAsync(user) &&
                twoFactorPolicy != null &&
                twoFactorPolicy.Enabled &&
                notExempt(orgUser))
            {
                throw new BadRequestException("You cannot join this organization until you enable " +
                    "two-step login on your user account.");
            }

            orgUser.Status = OrganizationUserStatusType.Accepted;
            orgUser.UserId = user.Id;
            orgUser.Email = null;

            await _organizationUserRepository.ReplaceAsync(orgUser);

            // TODO: send notification emails to org admins and accepting user?
            return orgUser;
        }

        public async Task<OrganizationUser> ConfirmUserAsync(Guid organizationId, Guid organizationUserId, string key,
            Guid confirmingUserId, IUserService userService)
        {
            var result = await ConfirmUsersAsync(organizationId, new Dictionary<Guid, string>() {{organizationUserId, key}},
                confirmingUserId, userService);

            if (!result.Any())
            {
                throw new BadRequestException("User not valid.");
            }

            var (orgUser, error) = result[0];
            if (error != "")
            {
                throw new BadRequestException(error);
            }
            return orgUser;
        }
        
        public async Task<List<Tuple<OrganizationUser, string>>> ConfirmUsersAsync(Guid organizationId, Dictionary<Guid, string> keys,
            Guid confirmingUserId, IUserService userService)
        {
            var organizationUsers = await _organizationUserRepository.GetManyAsync(keys.Keys);
            var validOrganizationUsers = organizationUsers
                .Where(u => u.Status == OrganizationUserStatusType.Accepted && u.OrganizationId == organizationId && u.UserId != null)
                .ToList();

            if (!validOrganizationUsers.Any())
            {
                return new List<Tuple<OrganizationUser, string>>();
            }

            var validOrganizationUserIds = validOrganizationUsers.Select(u => u.UserId.Value).ToList();
            
            var organization = await GetOrgById(organizationId);
            var policies = await _policyRepository.GetManyByOrganizationIdAsync(organizationId);
            var usersOrgs = await _organizationUserRepository.GetManyByManyUsersAsync(validOrganizationUserIds);
            var users = await _userRepository.GetManyAsync(validOrganizationUserIds);

            var keyedFilteredUsers = validOrganizationUsers.ToDictionary(u => u.UserId.Value, u => u);
            var keyedOrganizationUsers = usersOrgs.GroupBy(u => u.UserId.Value)
                .ToDictionary(u => u.Key, u => u.ToList());

            var succeededUsers = new List<OrganizationUser>();
            var result = new List<Tuple<OrganizationUser, string>>();

            foreach (var user in users)
            {
                if (!keyedFilteredUsers.ContainsKey(user.Id))
                {
                    continue;
                }
                var orgUser = keyedFilteredUsers[user.Id];
                var orgUsers = keyedOrganizationUsers.GetValueOrDefault(user.Id, new List<OrganizationUser>());
                try
                {
                    if (organization.PlanType == PlanType.Free && orgUser.Type == OrganizationUserType.Admin
                        || orgUser.Type == OrganizationUserType.Owner)
                    {
                        // Since free organizations only supports a few users there is not much point in avoiding N+1 queries for this.
                        var adminCount = await _organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(user.Id);
                        if (adminCount > 0)
                        {
                            throw new BadRequestException("User can only be an admin of one free organization.");
                        }
                    }

                    await CheckPolicies(policies, organizationId, user, orgUsers, userService);
                    orgUser.Status = OrganizationUserStatusType.Confirmed;
                    orgUser.Key = keys[orgUser.Id];
                    orgUser.Email = null;
                
                    await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
                    await _mailService.SendOrganizationConfirmedEmailAsync(organization.Name, user.Email);
                    await DeleteAndPushUserRegistrationAsync(organizationId, user.Id);
                    succeededUsers.Add(orgUser);
                    result.Add(Tuple.Create(orgUser, ""));
                }
                catch (BadRequestException e)
                {
                    result.Add(Tuple.Create(orgUser, e.Message));
                }
            }

            await _organizationUserRepository.ReplaceManyAsync(succeededUsers);

            return result;
        }

        private async Task CheckPolicies(ICollection<Policy> policies, Guid organizationId, User user,
            ICollection<OrganizationUser> userOrgs, IUserService userService)
        {
            var usingTwoFactorPolicy = policies.Any(p => p.Type == PolicyType.TwoFactorAuthentication && p.Enabled);
            if (usingTwoFactorPolicy && !await userService.TwoFactorIsEnabledAsync(user))
            {
                throw new BadRequestException("User does not have two-step login enabled.");
            }

            var usingSingleOrgPolicy = policies.Any(p => p.Type == PolicyType.SingleOrg && p.Enabled);
            if (usingSingleOrgPolicy)
            {
                if (userOrgs.Any(ou => ou.OrganizationId != organizationId && ou.Status != OrganizationUserStatusType.Invited))
                {
                    throw new BadRequestException("User is a member of another organization.");
                }
            }
        }

        public async Task SaveUserAsync(OrganizationUser user, Guid? savingUserId,
            IEnumerable<SelectionReadOnly> collections)
        {
            if (user.Id.Equals(default(Guid)))
            {
                throw new BadRequestException("Invite the user first.");
            }

            var originalUser = await _organizationUserRepository.GetByIdAsync(user.Id);
            if (user.Equals(originalUser))
            {
                throw new BadRequestException("Please make changes before saving.");
            }

            if (savingUserId.HasValue)
            {
                await ValidateOrganizationUserUpdatePermissionsAsync(savingUserId.Value, user.OrganizationId, user.Type, originalUser.Type);
            }

            if (user.Type != OrganizationUserType.Owner &&
                !await HasConfirmedOwnersExceptAsync(user.OrganizationId, new[] {user.Id}))
            {
                throw new BadRequestException("Organization must have at least one confirmed owner.");
            }

            if (user.AccessAll)
            {
                // We don't need any collections if we're flagged to have all access.
                collections = new List<SelectionReadOnly>();
            }
            await _organizationUserRepository.ReplaceAsync(user, collections);
            await _eventService.LogOrganizationUserEventAsync(user, EventType.OrganizationUser_Updated);
        }

        public async Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId)
        {
            var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
            if (orgUser == null || orgUser.OrganizationId != organizationId)
            {
                throw new BadRequestException("User not valid.");
            }

            if (deletingUserId.HasValue && orgUser.UserId == deletingUserId.Value)
            {
                throw new BadRequestException("You cannot remove yourself.");
            }

            if (orgUser.Type == OrganizationUserType.Owner && deletingUserId.HasValue &&
                !await UserIsOwnerAsync(organizationId, deletingUserId.Value))
            {
                throw new BadRequestException("Only owners can delete other owners.");
            }

            if (!await HasConfirmedOwnersExceptAsync(organizationId, new[] {organizationUserId}))
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

        public async Task DeleteUserAsync(Guid organizationId, Guid userId)
        {
            var orgUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId, userId);
            if (orgUser == null)
            {
                throw new NotFoundException();
            }

            if (!await HasConfirmedOwnersExceptAsync(organizationId, new[] {orgUser.Id}))
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

        public async Task<List<Tuple<OrganizationUser, string>>> DeleteUsersAsync(Guid organizationId,
            IEnumerable<Guid> organizationUsersId,
            Guid? deletingUserId)
        {
            var orgUsers = await _organizationUserRepository.GetManyAsync(organizationUsersId);
            var filteredUsers = orgUsers.Where(u => u.OrganizationId == organizationId)
                .ToList();

            if (!filteredUsers.Any())
            {
                throw new BadRequestException("Users invalid.");
            }

            if (!await HasConfirmedOwnersExceptAsync(organizationId, organizationUsersId))
            {
                throw new BadRequestException("Organization must have at least one confirmed owner.");
            }

            var deletingUserIsOwner = false;
            if (deletingUserId.HasValue)
            {
                deletingUserIsOwner = await UserIsOwnerAsync(organizationId, deletingUserId.Value);
            }

            var result = new List<Tuple<OrganizationUser, string>>();
            var deletedUserIds = new List<Guid>();
            foreach (var orgUser in filteredUsers)
            {
                try
                {
                    if (deletingUserId.HasValue && orgUser.UserId == deletingUserId)
                    {
                        throw new BadRequestException("You cannot remove yourself.");
                    }

                    if (orgUser.Type == OrganizationUserType.Owner && deletingUserId.HasValue && !deletingUserIsOwner)
                    {
                        throw new BadRequestException("Only owners can delete other owners.");
                    }

                    await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Removed);

                    if (orgUser.UserId.HasValue)
                    {
                        await DeleteAndPushUserRegistrationAsync(organizationId, orgUser.UserId.Value);
                    }
                    result.Add(Tuple.Create(orgUser, ""));
                    deletedUserIds.Add(orgUser.Id);
                }
                catch (BadRequestException e)
                {
                    result.Add(Tuple.Create(orgUser, e.Message));
                }

                await _organizationUserRepository.DeleteManyAsync(deletedUserIds);
            }

            return result;
        }

        private async Task<bool> HasConfirmedOwnersExceptAsync(Guid organizationId, IEnumerable<Guid> organizationUsersId)
        {
            var confirmedOwners = await GetConfirmedOwnersAsync(organizationId);
            var confirmedOwnersIds = confirmedOwners.Select(u => u.Id);
            return confirmedOwnersIds.Except(organizationUsersId).Any();
        }

        private async Task<bool> UserIsOwnerAsync(Guid organizationId, Guid deletingUserId)
        {
            var deletingUserOrgs = await _organizationUserRepository.GetManyByUserAsync(deletingUserId);
            return deletingUserOrgs.Any(u => u.OrganizationId == organizationId && u.Type == OrganizationUserType.Owner);
        }

        public async Task UpdateUserGroupsAsync(OrganizationUser organizationUser, IEnumerable<Guid> groupIds, Guid? loggedInUserId)
        {
            if (loggedInUserId.HasValue)
            {
                await ValidateOrganizationUserUpdatePermissionsAsync(loggedInUserId.Value, organizationUser.OrganizationId, organizationUser.Type, null);
            }
            await _organizationUserRepository.UpdateGroupsAsync(organizationUser.Id, groupIds);
            await _eventService.LogOrganizationUserEventAsync(organizationUser,
                EventType.OrganizationUser_UpdatedGroups);
        }

        public async Task UpdateUserResetPasswordEnrollmentAsync(Guid organizationId, Guid organizationUserId, string resetPasswordKey, Guid? callingUserId)
        {
            // Org User must be the same as the calling user and the organization ID associated with the user must match passed org ID
            var orgUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId, organizationUserId);
            if (!callingUserId.HasValue || orgUser == null || orgUser.UserId != callingUserId.Value || 
                orgUser.OrganizationId != organizationId)
            {
                throw new BadRequestException("User not valid.");
            }
            
            // Make sure the organization has the ability to use password reset
            var org = await _organizationRepository.GetByIdAsync(organizationId);
            if (org == null || !org.UseResetPassword)
            {
                throw new BadRequestException("Organization does not allow password reset enrollment.");
            }

            // Make sure the organization has the policy enabled
            var resetPasswordPolicy =
                await _policyRepository.GetByOrganizationIdTypeAsync(organizationId, PolicyType.ResetPassword);
            if (resetPasswordPolicy == null || !resetPasswordPolicy.Enabled)
            {
                throw new BadRequestException("Organization does not have the password reset policy enabled.");
            }
            
            orgUser.ResetPasswordKey = resetPasswordKey;
            await _organizationUserRepository.ReplaceAsync(orgUser);
            await _eventService.LogOrganizationUserEventAsync(orgUser, resetPasswordKey != null ?
                EventType.OrganizationUser_ResetPassword_Enroll : EventType.OrganizationUser_ResetPassword_Withdraw);
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

        public async Task<OrganizationUser> InviteUserAsync(Guid organizationId, Guid? invitingUserId, string email,
            OrganizationUserType type, bool accessAll, string externalId, IEnumerable<SelectionReadOnly> collections)
        {
            var invite = new OrganizationUserInvite()
            {
                Emails = new List<string> { email },
                Type = type,
                AccessAll = accessAll,
                Collections = collections,
            };
            var results = await InviteUserAsync(organizationId, invitingUserId, externalId, invite);
            var result = results.FirstOrDefault();
            if (result == null)
            {
                throw new BadRequestException("This user has already been invited.");
            }
            return result;
        }

        public async Task ImportAsync(Guid organizationId,
            Guid? importingUserId,
            IEnumerable<ImportedGroup> groups,
            IEnumerable<ImportedOrganizationUser> newUsers,
            IEnumerable<string> removeUserExternalIds,
            bool overwriteExisting)
        {
            var organization = await GetOrgById(organizationId);
            if (organization == null)
            {
                throw new NotFoundException();
            }

            if (!organization.UseDirectory)
            {
                throw new BadRequestException("Organization cannot use directory syncing.");
            }

            var newUsersSet = new HashSet<string>(newUsers?.Select(u => u.ExternalId) ?? new List<string>());
            var existingUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);
            var existingExternalUsers = existingUsers.Where(u => !string.IsNullOrWhiteSpace(u.ExternalId)).ToList();
            var existingExternalUsersIdDict = existingExternalUsers.ToDictionary(u => u.ExternalId, u => u.Id);

            // Users

            // Remove Users
            if (removeUserExternalIds?.Any() ?? false)
            {
                var removeUsersSet = new HashSet<string>(removeUserExternalIds);
                var existingUsersDict = existingExternalUsers.ToDictionary(u => u.ExternalId);

                await _organizationUserRepository.DeleteManyAsync(removeUsersSet
                    .Except(newUsersSet)
                    .Where(u => existingUsersDict.ContainsKey(u) && existingUsersDict[u].Type != OrganizationUserType.Owner)
                    .Select(u => existingUsersDict[u].Id));
            }

            if (overwriteExisting)
            {
                // Remove existing external users that are not in new user set
                var usersToDelete = existingExternalUsers.Where(u =>
                    u.Type != OrganizationUserType.Owner &&
                    !newUsersSet.Contains(u.ExternalId) &&
                    existingExternalUsersIdDict.ContainsKey(u.ExternalId));
                await _organizationUserRepository.DeleteManyAsync(usersToDelete.Select(u => u.Id));
                foreach (var deletedUser in usersToDelete)
                {
                    existingExternalUsersIdDict.Remove(deletedUser.ExternalId);
                }
            }

            if (newUsers?.Any() ?? false)
            {
                // Marry existing users
                var existingUsersEmailsDict = existingUsers
                    .Where(u => string.IsNullOrWhiteSpace(u.ExternalId))
                    .ToDictionary(u => u.Email);
                var newUsersEmailsDict = newUsers.ToDictionary(u => u.Email);
                var usersToAttach = existingUsersEmailsDict.Keys.Intersect(newUsersEmailsDict.Keys).ToList();
                var usersToUpsert = new List<OrganizationUser>();
                foreach (var user in usersToAttach)
                {
                    var orgUserDetails = existingUsersEmailsDict[user];
                    var orgUser = await _organizationUserRepository.GetByIdAsync(orgUserDetails.Id);
                    if (orgUser != null)
                    {
                        orgUser.ExternalId = newUsersEmailsDict[user].ExternalId;
                        usersToUpsert.Add(orgUser);
                        existingExternalUsersIdDict.Add(orgUser.ExternalId, orgUser.Id);
                    }
                }
                await _organizationUserRepository.UpsertManyAsync(usersToUpsert);

                // Add new users
                var existingUsersSet = new HashSet<string>(existingExternalUsersIdDict.Keys);
                var usersToAdd = newUsersSet.Except(existingUsersSet).ToList();

                var seatsAvailable = int.MaxValue;
                var enoughSeatsAvailable = true;
                if (organization.Seats.HasValue)
                {
                    var userCount = await _organizationUserRepository.GetCountByOrganizationIdAsync(organizationId);
                    seatsAvailable = organization.Seats.Value - userCount;
                    enoughSeatsAvailable = seatsAvailable >= usersToAdd.Count;
                }

                if (!enoughSeatsAvailable)
                {
                    throw new BadRequestException($"Organization does not have enough seats available. Need {usersToAdd.Count} but {seatsAvailable} available.");
                }

                var userInvites = new List<(OrganizationUserInvite, string)>();
                foreach (var user in newUsers)
                {
                    if (!usersToAdd.Contains(user.ExternalId) || string.IsNullOrWhiteSpace(user.Email))
                    {
                        continue;
                    }

                    try
                    {
                        var invite = new OrganizationUserInvite
                        {
                            Emails = new List<string> { user.Email },
                            Type = OrganizationUserType.User,
                            AccessAll = false,
                            Collections = new List<SelectionReadOnly>(),
                        };
                        userInvites.Add((invite, user.ExternalId));
                    }
                    catch (BadRequestException)
                    {
                        // Thrown when the user is already invited to the organization
                        continue;
                    }
                }

                var invitedUsers = await InviteUsersAsync(organizationId, importingUserId, userInvites);
                foreach (var invitedUser in invitedUsers)
                {
                    existingExternalUsersIdDict.Add(invitedUser.ExternalId, invitedUser.Id);
                }
            }


            // Groups
            if (groups?.Any() ?? false)
            {
                if (!organization.UseGroups)
                {
                    throw new BadRequestException("Organization cannot use groups.");
                }

                var groupsDict = groups.ToDictionary(g => g.Group.ExternalId);
                var existingGroups = await _groupRepository.GetManyByOrganizationIdAsync(organizationId);
                var existingExternalGroups = existingGroups
                    .Where(u => !string.IsNullOrWhiteSpace(u.ExternalId)).ToList();
                var existingExternalGroupsDict = existingExternalGroups.ToDictionary(g => g.ExternalId);

                var newGroups = groups
                    .Where(g => !existingExternalGroupsDict.ContainsKey(g.Group.ExternalId))
                    .Select(g => g.Group);

                foreach (var group in newGroups)
                {
                    group.CreationDate = group.RevisionDate = DateTime.UtcNow;

                    await _groupRepository.CreateAsync(group);
                    await UpdateUsersAsync(group, groupsDict[group.ExternalId].ExternalUserIds,
                        existingExternalUsersIdDict);
                }

                var updateGroups = existingExternalGroups
                    .Where(g => groupsDict.ContainsKey(g.ExternalId))
                    .ToList();

                if (updateGroups.Any())
                {
                    var groupUsers = await _groupRepository.GetManyGroupUsersByOrganizationIdAsync(organizationId);
                    var existingGroupUsers = groupUsers
                        .GroupBy(gu => gu.GroupId)
                        .ToDictionary(g => g.Key, g => new HashSet<Guid>(g.Select(gr => gr.OrganizationUserId)));

                    foreach (var group in updateGroups)
                    {
                        var updatedGroup = groupsDict[group.ExternalId].Group;
                        if (group.Name != updatedGroup.Name)
                        {
                            group.RevisionDate = DateTime.UtcNow;
                            group.Name = updatedGroup.Name;

                            await _groupRepository.ReplaceAsync(group);
                        }

                        await UpdateUsersAsync(group, groupsDict[group.ExternalId].ExternalUserIds,
                            existingExternalUsersIdDict,
                            existingGroupUsers.ContainsKey(group.Id) ? existingGroupUsers[group.Id] : null);
                    }
                }
            }
        }

        public async Task RotateApiKeyAsync(Organization organization)
        {
            organization.ApiKey = CoreHelpers.SecureRandomString(30);
            organization.RevisionDate = DateTime.UtcNow;
            await ReplaceAndUpdateCache(organization);
        }

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

        public async Task<Organization> UpdateOrganizationKeysAsync(Guid userId, Guid orgId, string publicKey, string privateKey)
        {
            // Only Owners/Admins/Custom (w/ ManageResetPassword) can create org keys
            var orgUser = await _organizationUserRepository.GetDetailsByUserAsync(userId, orgId);
            if (orgUser == null || orgUser.Type != OrganizationUserType.Admin && 
                orgUser.Type != OrganizationUserType.Owner && orgUser.Type != OrganizationUserType.Custom)
            {
                throw new UnauthorizedAccessException();
            }

            if (orgUser.Type == OrganizationUserType.Custom)
            {
                var permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(orgUser.Permissions);
                if (permissions == null || !permissions.ManageResetPassword)
                {
                    throw new UnauthorizedAccessException();
                }
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

        private async Task UpdateUsersAsync(Group group, HashSet<string> groupUsers,
            Dictionary<string, Guid> existingUsersIdDict, HashSet<Guid> existingUsers = null)
        {
            var availableUsers = groupUsers.Intersect(existingUsersIdDict.Keys);
            var users = new HashSet<Guid>(availableUsers.Select(u => existingUsersIdDict[u]));
            if (existingUsers != null && existingUsers.Count == users.Count && users.SetEquals(existingUsers))
            {
                return;
            }

            await _groupRepository.UpdateUsersAsync(group.Id, users);
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

        private async Task ReplaceAndUpdateCache(Organization org, EventType? orgEvent = null)
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

        private void ValidateOrganizationUpgradeParameters(Models.StaticStore.Plan plan, OrganizationUpgrade upgrade)
        {
            if (!plan.HasAdditionalStorageOption && upgrade.AdditionalStorageGb > 0)
            {
                throw new BadRequestException("Plan does not allow additional storage.");
            }

            if (upgrade.AdditionalStorageGb < 0)
            {
                throw new BadRequestException("You can't subtract storage!");
            }

            if (!plan.HasPremiumAccessOption && upgrade.PremiumAccessAddon)
            {
                throw new BadRequestException("This plan does not allow you to buy the premium access addon.");
            }

            if (plan.BaseSeats + upgrade.AdditionalSeats <= 0)
            {
                throw new BadRequestException("You do not have any seats!");
            }

            if (upgrade.AdditionalSeats < 0)
            {
                throw new BadRequestException("You can't subtract seats!");
            }

            if (!plan.HasAdditionalSeatsOption && upgrade.AdditionalSeats > 0)
            {
                throw new BadRequestException("Plan does not allow additional users.");
            }

            if (plan.HasAdditionalSeatsOption && plan.MaxAdditionalSeats.HasValue &&
                upgrade.AdditionalSeats > plan.MaxAdditionalSeats.Value)
            {
                throw new BadRequestException($"Selected plan allows a maximum of " +
                    $"{plan.MaxAdditionalSeats.GetValueOrDefault(0)} additional users.");
            }
        }

        private async Task ValidateOrganizationUserUpdatePermissionsAsync(Guid loggedInUserId, Guid organizationId,
            OrganizationUserType newType, OrganizationUserType? oldType)
        {
            var loggedInUserOrgs = await _organizationUserRepository.GetManyByUserAsync(loggedInUserId);
            var loggedInAsOrgOwner = loggedInUserOrgs
                .Any(u => u.OrganizationId == organizationId && u.Type == OrganizationUserType.Owner);
            if (loggedInAsOrgOwner)
            {
                return;
            }

            var isOwner = oldType == OrganizationUserType.Owner;
            var nowOwner = newType == OrganizationUserType.Owner;
            var ownerUserConfigurationAttempt = (isOwner && nowOwner) || !(isOwner.Equals(nowOwner));
            if (ownerUserConfigurationAttempt)
            {
                throw new BadRequestException("Only an Owner can configure another Owner's account.");
            }

            var loggedInAsOrgAdmin = loggedInUserOrgs.Any(u => u.OrganizationId == organizationId && u.Type == OrganizationUserType.Admin);
            if (loggedInAsOrgAdmin)
            {
                return;
            }

            var isCustom = oldType == OrganizationUserType.Custom;
            var nowCustom = newType == OrganizationUserType.Custom;
            var customUserConfigurationAttempt = (isCustom && nowCustom) || !(isCustom.Equals(nowCustom));
            if (customUserConfigurationAttempt)
            {
                throw new BadRequestException("Only Owners and Admins can configure Custom accounts.");
            }

            var loggedInAsOrgCustom = loggedInUserOrgs.Any(u => u.OrganizationId == organizationId && u.Type == OrganizationUserType.Custom);
            if (!loggedInAsOrgCustom)
            {
                return;
            }

            var loggedInCustomOrgUser = loggedInUserOrgs.First(u => u.OrganizationId == organizationId && u.Type == OrganizationUserType.Custom);
            var loggedInUserPermissions = CoreHelpers.LoadClassFromJsonData<Permissions>(loggedInCustomOrgUser.Permissions);
            if (!loggedInUserPermissions.ManageUsers)
            {
                throw new BadRequestException("Your account does not have permission to manage users.");
            }

            var isAdmin = oldType == OrganizationUserType.Admin;
            var nowAdmin = newType == OrganizationUserType.Admin;
            var adminUserConfigurationAttempt = (isAdmin && nowAdmin) || !(isAdmin.Equals(nowAdmin));
            if (adminUserConfigurationAttempt)
            {
                throw new BadRequestException("Custom users can not manage Admins or Owners.");
            }
        }
    }
}
