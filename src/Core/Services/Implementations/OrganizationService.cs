using System.Security.Claims;
using System.Text.Json;
using Bit.Core.AdminConsole.Models.OrganizationConnectionConfigs;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Enums.Provider;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.Policies;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Services;

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
    private readonly IPolicyService _policyService;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly ISsoUserRepository _ssoUserRepository;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IGlobalSettings _globalSettings;
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;
    private readonly IOrganizationConnectionRepository _organizationConnectionRepository;
    private readonly ICurrentContext _currentContext;
    private readonly ILogger<OrganizationService> _logger;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly IProviderUserRepository _providerUserRepository;

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
        IPolicyService policyService,
        ISsoConfigRepository ssoConfigRepository,
        ISsoUserRepository ssoUserRepository,
        IReferenceEventService referenceEventService,
        IGlobalSettings globalSettings,
        IOrganizationApiKeyRepository organizationApiKeyRepository,
        IOrganizationConnectionRepository organizationConnectionRepository,
        ICurrentContext currentContext,
        ILogger<OrganizationService> logger,
        IProviderOrganizationRepository providerOrganizationRepository,
        IProviderUserRepository providerUserRepository)
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
        _policyService = policyService;
        _ssoConfigRepository = ssoConfigRepository;
        _ssoUserRepository = ssoUserRepository;
        _referenceEventService = referenceEventService;
        _globalSettings = globalSettings;
        _organizationApiKeyRepository = organizationApiKeyRepository;
        _organizationConnectionRepository = organizationConnectionRepository;
        _currentContext = currentContext;
        _logger = logger;
        _providerOrganizationRepository = providerOrganizationRepository;
        _providerUserRepository = providerUserRepository;
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
            await ReplaceAndUpdateCacheAsync(organization);
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
            new ReferenceEvent(ReferenceEventType.CancelSubscription, organization, _currentContext)
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
            new ReferenceEvent(ReferenceEventType.ReinstateSubscription, organization, _currentContext));
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
            var occupiedSeats = await _organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
            if (occupiedSeats > newPlanSeats)
            {
                throw new BadRequestException($"Your organization currently has {occupiedSeats} seats filled. " +
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

        if (!newPlan.HasKeyConnector && organization.UseKeyConnector)
        {
            var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
            if (ssoConfig != null && ssoConfig.GetData().MemberDecryptionType == MemberDecryptionType.KeyConnector)
            {
                throw new BadRequestException("Your new plan does not allow the Key Connector feature. " +
                                              "Disable your Key Connector.");
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

        if (!newPlan.HasScim && organization.UseScim)
        {
            var scimConnections = await _organizationConnectionRepository.GetByOrganizationIdTypeAsync(organization.Id,
                OrganizationConnectionType.Scim);
            if (scimConnections != null && scimConnections.Any(c => c.GetConfig<ScimConfig>()?.Enabled == true))
            {
                throw new BadRequestException("Your new plan does not allow the SCIM feature. " +
                    "Disable your SCIM configuration.");
            }
        }

        if (!newPlan.HasCustomPermissions && organization.UseCustomPermissions)
        {
            var organizationCustomUsers =
                await _organizationUserRepository.GetManyByOrganizationAsync(organization.Id,
                    OrganizationUserType.Custom);
            if (organizationCustomUsers.Any())
            {
                throw new BadRequestException("Your new plan does not allow the Custom Permissions feature. " +
                                              "Disable your Custom Permissions configuration.");
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
        organization.UseKeyConnector = newPlan.HasKeyConnector;
        organization.UseScim = newPlan.HasScim;
        organization.UseResetPassword = newPlan.HasResetPassword;
        organization.SelfHost = newPlan.HasSelfHost;
        organization.UsersGetPremium = newPlan.UsersGetPremium || upgrade.PremiumAccessAddon;
        organization.UseCustomPermissions = newPlan.HasCustomPermissions;
        organization.Plan = newPlan.Name;
        organization.Enabled = success;
        organization.PublicKey = upgrade.PublicKey;
        organization.PrivateKey = upgrade.PrivateKey;
        await ReplaceAndUpdateCacheAsync(organization);
        if (success)
        {
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.UpgradePlan, organization, _currentContext)
                {
                    PlanName = newPlan.Name,
                    PlanType = newPlan.Type,
                    OldPlanName = existingPlan.Name,
                    OldPlanType = existingPlan.Type,
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
            new ReferenceEvent(ReferenceEventType.AdjustStorage, organization, _currentContext)
            {
                PlanName = plan.Name,
                PlanType = plan.Type,
                Storage = storageAdjustmentGb,
            });
        await ReplaceAndUpdateCacheAsync(organization);
        return secret;
    }

    public async Task UpdateSubscription(Guid organizationId, int seatAdjustment, int? maxAutoscaleSeats)
    {
        var organization = await GetOrgById(organizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        var newSeatCount = organization.Seats + seatAdjustment;
        if (maxAutoscaleSeats.HasValue && newSeatCount > maxAutoscaleSeats.Value)
        {
            throw new BadRequestException("Cannot set max seat autoscaling below seat count.");
        }

        if (seatAdjustment != 0)
        {
            await AdjustSeatsAsync(organization, seatAdjustment);
        }
        if (maxAutoscaleSeats != organization.MaxAutoscaleSeats)
        {
            await UpdateAutoscalingAsync(organization, maxAutoscaleSeats);
        }
    }

    private async Task UpdateAutoscalingAsync(Organization organization, int? maxAutoscaleSeats)
    {

        if (maxAutoscaleSeats.HasValue &&
            organization.Seats.HasValue &&
            maxAutoscaleSeats.Value < organization.Seats.Value)
        {
            throw new BadRequestException($"Cannot set max seat autoscaling below current seat count.");
        }

        var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType);
        if (plan == null)
        {
            throw new BadRequestException("Existing plan not found.");
        }

        if (!plan.AllowSeatAutoscale)
        {
            throw new BadRequestException("Your plan does not allow seat autoscaling.");
        }

        if (plan.MaxUsers.HasValue && maxAutoscaleSeats.HasValue &&
            maxAutoscaleSeats > plan.MaxUsers)
        {
            throw new BadRequestException(string.Concat($"Your plan has a seat limit of {plan.MaxUsers}, ",
                $"but you have specified a max autoscale count of {maxAutoscaleSeats}.",
                "Reduce your max autoscale seat count."));
        }

        organization.MaxAutoscaleSeats = maxAutoscaleSeats;

        await ReplaceAndUpdateCacheAsync(organization);
    }

    public async Task<string> AdjustSeatsAsync(Guid organizationId, int seatAdjustment, DateTime? prorationDate = null)
    {
        var organization = await GetOrgById(organizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        return await AdjustSeatsAsync(organization, seatAdjustment, prorationDate);
    }

    private async Task<string> AdjustSeatsAsync(Organization organization, int seatAdjustment, DateTime? prorationDate = null, IEnumerable<string> ownerEmails = null)
    {
        if (organization.Seats == null)
        {
            throw new BadRequestException("Organization has no seat limit, no need to adjust seats");
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

        var newSeatTotal = organization.Seats.Value + seatAdjustment;
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
            var occupiedSeats = await _organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
            if (occupiedSeats > newSeatTotal)
            {
                throw new BadRequestException($"Your organization currently has {occupiedSeats} seats filled. " +
                    $"Your new plan only has ({newSeatTotal}) seats. Remove some users.");
            }
        }

        var paymentIntentClientSecret = await _paymentService.AdjustSeatsAsync(organization, plan, additionalSeats, prorationDate);
        await _referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.AdjustSeats, organization, _currentContext)
            {
                PlanName = plan.Name,
                PlanType = plan.Type,
                Seats = newSeatTotal,
                PreviousSeats = organization.Seats
            });
        organization.Seats = (short?)newSeatTotal;
        await ReplaceAndUpdateCacheAsync(organization);

        if (organization.Seats.HasValue && organization.MaxAutoscaleSeats.HasValue && organization.Seats == organization.MaxAutoscaleSeats)
        {
            try
            {
                if (ownerEmails == null)
                {
                    ownerEmails = (await _organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id,
                        OrganizationUserType.Owner)).Select(u => u.Email).Distinct();
                }
                await _mailService.SendOrganizationMaxSeatLimitReachedEmailAsync(organization, organization.MaxAutoscaleSeats.Value, ownerEmails);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error encountered notifying organization owners of seat limit reached.");
            }
        }

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
        var customer = await customerService.GetAsync(organization.GatewayCustomerId,
            new CustomerGetOptions { Expand = new List<string> { "sources" } });
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
        if (plan is not { LegacyYear: null })
        {
            throw new BadRequestException("Invalid plan selected.");
        }

        if (plan.Disabled)
        {
            throw new BadRequestException("Plan not found.");
        }

        if (!provider)
        {
            await ValidateSignUpPoliciesAsync(signup.Owner.Id);
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
            UseCustomPermissions = plan.HasCustomPermissions,
            UseScim = plan.HasScim,
            Plan = plan.Name,
            Gateway = null,
            ReferenceData = signup.Owner.ReferenceData,
            Enabled = true,
            LicenseKey = CoreHelpers.SecureRandomString(20),
            PublicKey = signup.PublicKey,
            PrivateKey = signup.PrivateKey,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
            Status = OrganizationStatusType.Created
        };

        if (plan.Type == PlanType.Free && !provider)
        {
            var adminCount =
                await _organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(signup.Owner.Id);
            if (adminCount > 0)
            {
                throw new BadRequestException("You can only be an admin of one free organization.");
            }
        }
        else if (plan.Type != PlanType.Free)
        {
            await _paymentService.PurchaseOrganizationAsync(organization, signup.PaymentMethodType.Value,
                signup.PaymentToken, plan, signup.AdditionalStorageGb, signup.AdditionalSeats,
                signup.PremiumAccessAddon, signup.TaxInfo, provider);
        }

        var ownerId = provider ? default : signup.Owner.Id;
        var returnValue = await SignUpAsync(organization, ownerId, signup.OwnerKey, signup.CollectionName, true);
        await _referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.Signup, organization, _currentContext)
            {
                PlanName = plan.Name,
                PlanType = plan.Type,
                Seats = returnValue.Item1.Seats,
                Storage = returnValue.Item1.MaxStorageGb,
            });
        return returnValue;
    }

    private async Task ValidateSignUpPoliciesAsync(Guid ownerId)
    {
        var anySingleOrgPolicies = await _policyService.AnyPoliciesApplicableToUserAsync(ownerId, PolicyType.SingleOrg);
        if (anySingleOrgPolicies)
        {
            throw new BadRequestException("You may not create an organization. You belong to an organization " +
                "which has a policy that prohibits you from being a member of any other organization.");
        }
    }

    public async Task<Tuple<Organization, OrganizationUser>> SignUpAsync(
        OrganizationLicense license, User owner, string ownerKey, string collectionName, string publicKey,
        string privateKey)
    {
        var canUse = license.CanUse(_globalSettings, _licensingService, out var exception);
        if (!canUse)
        {
            throw new BadRequestException(exception);
        }

        if (license.PlanType != PlanType.Custom &&
            StaticStore.Plans.FirstOrDefault(p => p.Type == license.PlanType && !p.Disabled) == null)
        {
            throw new BadRequestException("Plan not found.");
        }

        var enabledOrgs = await _organizationRepository.GetManyByEnabledAsync();
        if (enabledOrgs.Any(o => string.Equals(o.LicenseKey, license.LicenseKey)))
        {
            throw new BadRequestException("License is already in use by another organization.");
        }

        await ValidateSignUpPoliciesAsync(owner.Id);

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
            UseScim = license.UseScim,
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
            UseCustomPermissions = license.UseCustomPermissions,
            Gateway = null,
            GatewayCustomerId = null,
            GatewaySubscriptionId = null,
            ReferenceData = owner.ReferenceData,
            Enabled = license.Enabled,
            ExpirationDate = license.Expires,
            LicenseKey = license.LicenseKey,
            PublicKey = publicKey,
            PrivateKey = privateKey,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
            Status = OrganizationStatusType.Created
        };

        var result = await SignUpAsync(organization, owner.Id, ownerKey, collectionName, false);

        var dir = $"{_globalSettings.LicenseDirectory}/organization";
        Directory.CreateDirectory(dir);
        await using var fs = new FileStream(Path.Combine(dir, $"{organization.Id}.json"), FileMode.Create);
        await JsonSerializer.SerializeAsync(fs, license, JsonHelpers.Indented);
        return result;
    }

    private async Task<Tuple<Organization, OrganizationUser>> SignUpAsync(Organization organization,
        Guid ownerId, string ownerKey, string collectionName, bool withPayment)
    {
        try
        {
            await _organizationRepository.CreateAsync(organization);
            await _organizationApiKeyRepository.CreateAsync(new OrganizationApiKey
            {
                OrganizationId = organization.Id,
                ApiKey = CoreHelpers.SecureRandomString(30),
                Type = OrganizationApiKeyType.Default,
                RevisionDate = DateTime.UtcNow,
            });
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
                    new ReferenceEvent(ReferenceEventType.DeleteAccount, organization, _currentContext));
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
            await ReplaceAndUpdateCacheAsync(org);
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
            await ReplaceAndUpdateCacheAsync(org);

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
            await ReplaceAndUpdateCacheAsync(org);
        }
    }

    public async Task EnableAsync(Guid organizationId)
    {
        var org = await GetOrgById(organizationId);
        if (org != null && !org.Enabled)
        {
            org.Enabled = true;
            await ReplaceAndUpdateCacheAsync(org);
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

        await ReplaceAndUpdateCacheAsync(organization, EventType.Organization_Updated);

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

            await SendInviteAsync(orgUser, org, false);
            result.Add(Tuple.Create(orgUser, ""));
        }

        return result;
    }

    public async Task ResendInviteAsync(Guid organizationId, Guid? invitingUserId, Guid organizationUserId, bool initOrganization = false)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (orgUser == null || orgUser.OrganizationId != organizationId ||
            orgUser.Status != OrganizationUserStatusType.Invited)
        {
            throw new BadRequestException("User invalid.");
        }

        var org = await GetOrgById(orgUser.OrganizationId);
        await SendInviteAsync(orgUser, org, initOrganization);
    }

    private async Task SendInviteAsync(OrganizationUser orgUser, Organization organization, bool initOrganization)
    {
        var now = DateTime.UtcNow;
        var nowMillis = CoreHelpers.ToEpocMilliseconds(now);
        var token = _dataProtector.Protect(
            $"OrganizationUserInvite {orgUser.Id} {orgUser.Email} {nowMillis}");
        await _mailService.SendOrganizationInviteEmailAsync(organization.Name, orgUser, new ExpiringToken(token, now.AddDays(5)), organization.PlanType == PlanType.Free, initOrganization);
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
        if (orgUser.Status == OrganizationUserStatusType.Revoked)
        {
            throw new BadRequestException("Your organization access has been revoked.");
        }

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

        // Enforce Single Organization Policy of organization user is trying to join
        var allOrgUsers = await _organizationUserRepository.GetManyByUserAsync(user.Id);
        var hasOtherOrgs = allOrgUsers.Any(ou => ou.OrganizationId != orgUser.OrganizationId);
        var invitedSingleOrgPolicies = await _policyService.GetPoliciesApplicableToUserAsync(user.Id,
            PolicyType.SingleOrg, OrganizationUserStatusType.Invited);

        if (hasOtherOrgs && invitedSingleOrgPolicies.Any(p => p.OrganizationId == orgUser.OrganizationId))
        {
            throw new BadRequestException("You may not join this organization until you leave or remove " +
                "all other organizations.");
        }

        // Enforce Single Organization Policy of other organizations user is a member of
        var anySingleOrgPolicies = await _policyService.AnyPoliciesApplicableToUserAsync(user.Id,
            PolicyType.SingleOrg);
        if (anySingleOrgPolicies)
        {
            throw new BadRequestException("You cannot join this organization because you are a member of " +
                "another organization which forbids it");
        }

        // Enforce Two Factor Authentication Policy of organization user is trying to join
        if (!await userService.TwoFactorIsEnabledAsync(user))
        {
            var invitedTwoFactorPolicies = await _policyService.GetPoliciesApplicableToUserAsync(user.Id,
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

        var admins = await _organizationUserRepository.GetManyByMinimumRoleAsync(orgUser.OrganizationId, OrganizationUserType.Admin);
        var adminEmails = admins.Select(a => a.Email).Distinct().ToList();

        if (adminEmails.Count > 0)
        {
            var organization = await _organizationRepository.GetByIdAsync(orgUser.OrganizationId);
            await _mailService.SendOrganizationAcceptedEmailAsync(organization, user.Email, adminEmails);
        }

        return orgUser;
    }

    public async Task<OrganizationUser> ConfirmUserAsync(Guid organizationId, Guid organizationUserId, string key,
        Guid confirmingUserId, IUserService userService)
    {
        var result = await ConfirmUsersAsync(organizationId, new Dictionary<Guid, string>() { { organizationUserId, key } },
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
                if (organization.PlanType == PlanType.Free && (orgUser.Type == OrganizationUserType.Admin
                    || orgUser.Type == OrganizationUserType.Owner))
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

    internal (bool canScale, string failureReason) CanScale(Organization organization,
        int seatsToAdd)
    {
        var failureReason = "";
        if (_globalSettings.SelfHosted)
        {
            failureReason = "Cannot autoscale on self-hosted instance.";
            return (false, failureReason);
        }

        if (seatsToAdd < 1)
        {
            return (true, failureReason);
        }

        if (organization.Seats.HasValue &&
            organization.MaxAutoscaleSeats.HasValue &&
            organization.MaxAutoscaleSeats.Value < organization.Seats.Value + seatsToAdd)
        {
            return (false, $"Seat limit has been reached.");
        }

        return (true, failureReason);
    }

    public async Task AutoAddSeatsAsync(Organization organization, int seatsToAdd, DateTime? prorationDate = null)
    {
        if (seatsToAdd < 1 || !organization.Seats.HasValue)
        {
            return;
        }

        var (canScale, failureMessage) = CanScale(organization, seatsToAdd);
        if (!canScale)
        {
            throw new BadRequestException(failureMessage);
        }

        var providerOrg = await this._providerOrganizationRepository.GetByOrganizationId(organization.Id);

        IEnumerable<string> ownerEmails;
        if (providerOrg != null)
        {
            ownerEmails = (await _providerUserRepository.GetManyDetailsByProviderAsync(providerOrg.ProviderId, ProviderUserStatusType.Confirmed))
                .Select(u => u.Email).Distinct();
        }
        else
        {
            ownerEmails = (await _organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id,
                OrganizationUserType.Owner)).Select(u => u.Email).Distinct();
        }
        var initialSeatCount = organization.Seats.Value;

        await AdjustSeatsAsync(organization, seatsToAdd, prorationDate, ownerEmails);

        if (!organization.OwnersNotifiedOfAutoscaling.HasValue)
        {
            await _mailService.SendOrganizationAutoscaledEmailAsync(organization, initialSeatCount,
                ownerEmails);
            organization.OwnersNotifiedOfAutoscaling = DateTime.UtcNow;
            await _organizationRepository.UpsertAsync(organization);
        }
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

    [Obsolete("IDeleteOrganizationUserCommand should be used instead. To be removed by EC-607.")]
    public async Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId)
    {
        var orgUser = await RepositoryDeleteUserAsync(organizationId, organizationUserId, deletingUserId);
        await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Removed);
    }

    [Obsolete("IDeleteOrganizationUserCommand should be used instead. To be removed by EC-607.")]
    public async Task DeleteUserAsync(Guid organizationId, Guid organizationUserId,
        EventSystemUser systemUser)
    {
        var orgUser = await RepositoryDeleteUserAsync(organizationId, organizationUserId, null);
        await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Removed, systemUser);
    }

    private async Task<OrganizationUser> RepositoryDeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId)
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
            !await _currentContext.OrganizationOwner(organizationId))
        {
            throw new BadRequestException("Only owners can delete other owners.");
        }

        if (!await HasConfirmedOwnersExceptAsync(organizationId, new[] { organizationUserId }, includeProvider: true))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }

        await _organizationUserRepository.DeleteAsync(orgUser);

        if (orgUser.UserId.HasValue)
        {
            await DeleteAndPushUserRegistrationAsync(organizationId, orgUser.UserId.Value);
        }

        return orgUser;
    }

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
            deletingUserIsOwner = await _currentContext.OrganizationOwner(organizationId);
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

    public async Task<bool> HasConfirmedOwnersExceptAsync(Guid organizationId, IEnumerable<Guid> organizationUsersId, bool includeProvider = true)
    {
        var confirmedOwners = await GetConfirmedOwnersAsync(organizationId);
        var confirmedOwnersIds = confirmedOwners.Select(u => u.Id);
        bool hasOtherOwner = confirmedOwnersIds.Except(organizationUsersId).Any();
        if (!hasOtherOwner && includeProvider)
        {
            return (await _providerUserRepository.GetManyByOrganizationAsync(organizationId, ProviderUserStatusType.Confirmed)).Any();
        }
        return hasOtherOwner;
    }

    public async Task UpdateUserResetPasswordEnrollmentAsync(Guid organizationId, Guid userId, string resetPasswordKey, Guid? callingUserId)
    {
        // Org User must be the same as the calling user and the organization ID associated with the user must match passed org ID
        var orgUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId, userId);
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

        // Block the user from withdrawal if auto enrollment is enabled
        if (resetPasswordKey == null && resetPasswordPolicy.Data != null)
        {
            var data = JsonSerializer.Deserialize<ResetPasswordDataModel>(resetPasswordPolicy.Data, JsonHelpers.IgnoreCase);

            if (data?.AutoEnrollEnabled ?? false)
            {
                throw new BadRequestException("Due to an Enterprise Policy, you are not allowed to withdraw from Password Reset.");
            }
        }

        orgUser.ResetPasswordKey = resetPasswordKey;
        await _organizationUserRepository.ReplaceAsync(orgUser);
        await _eventService.LogOrganizationUserEventAsync(orgUser, resetPasswordKey != null ?
            EventType.OrganizationUser_ResetPassword_Enroll : EventType.OrganizationUser_ResetPassword_Withdraw);
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

    public async Task ReplaceAndUpdateCacheAsync(Organization org, EventType? orgEvent = null)
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

    private async Task<bool> ValidateCustomPermissionsGrant(Guid organizationId, Permissions permissions)
    {
        if (permissions == null || await _currentContext.OrganizationOwner(organizationId) || await _currentContext.OrganizationAdmin(organizationId))
        {
            return true;
        }

        if (permissions.ManageUsers && !await _currentContext.ManageUsers(organizationId))
        {
            return false;
        }

        if (permissions.AccessReports && !await _currentContext.AccessReports(organizationId))
        {
            return false;
        }

        if (permissions.ManageGroups && !await _currentContext.ManageGroups(organizationId))
        {
            return false;
        }

        if (permissions.ManagePolicies && !await _currentContext.ManagePolicies(organizationId))
        {
            return false;
        }

        if (permissions.ManageScim && !await _currentContext.ManageScim(organizationId))
        {
            return false;
        }

        if (permissions.ManageSso && !await _currentContext.ManageSso(organizationId))
        {
            return false;
        }

        if (permissions.AccessEventLogs && !await _currentContext.AccessEventLogs(organizationId))
        {
            return false;
        }

        if (permissions.AccessImportExport && !await _currentContext.AccessImportExport(organizationId))
        {
            return false;
        }

        if (permissions.CreateNewCollections && !await _currentContext.CreateNewCollections(organizationId))
        {
            return false;
        }

        if (permissions.DeleteAnyCollection && !await _currentContext.DeleteAnyCollection(organizationId))
        {
            return false;
        }

        if (permissions.DeleteAssignedCollections && !await _currentContext.DeleteAssignedCollections(organizationId))
        {
            return false;
        }

        if (permissions.EditAnyCollection && !await _currentContext.EditAnyCollection(organizationId))
        {
            return false;
        }

        if (permissions.EditAssignedCollections && !await _currentContext.EditAssignedCollections(organizationId))
        {
            return false;
        }

        if (permissions.ManageResetPassword && !await _currentContext.ManageResetPassword(organizationId))
        {
            return false;
        }

        return true;
    }

    private async Task ValidateDeleteOrganizationAsync(Organization organization)
    {
        var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
        if (ssoConfig?.GetData()?.MemberDecryptionType == MemberDecryptionType.KeyConnector)
        {
            throw new BadRequestException("You cannot delete an Organization that is using Key Connector.");
        }
    }

    public async Task RevokeUserAsync(OrganizationUser organizationUser, Guid? revokingUserId)
    {
        if (revokingUserId.HasValue && organizationUser.UserId == revokingUserId.Value)
        {
            throw new BadRequestException("You cannot revoke yourself.");
        }

        if (organizationUser.Type == OrganizationUserType.Owner && revokingUserId.HasValue &&
            !await _currentContext.OrganizationOwner(organizationUser.OrganizationId))
        {
            throw new BadRequestException("Only owners can revoke other owners.");
        }

        await RepositoryRevokeUserAsync(organizationUser);
        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Revoked);
    }

    public async Task RevokeUserAsync(OrganizationUser organizationUser,
        EventSystemUser systemUser)
    {
        await RepositoryRevokeUserAsync(organizationUser);
        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Revoked, systemUser);
    }

    private async Task RepositoryRevokeUserAsync(OrganizationUser organizationUser)
    {
        if (organizationUser.Status == OrganizationUserStatusType.Revoked)
        {
            throw new BadRequestException("Already revoked.");
        }

        if (!await HasConfirmedOwnersExceptAsync(organizationUser.OrganizationId, new[] { organizationUser.Id }, includeProvider: true))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }

        await _organizationUserRepository.RevokeAsync(organizationUser.Id);
        organizationUser.Status = OrganizationUserStatusType.Revoked;
    }

    public async Task<List<Tuple<OrganizationUser, string>>> RevokeUsersAsync(Guid organizationId,
        IEnumerable<Guid> organizationUserIds, Guid? revokingUserId)
    {
        var orgUsers = await _organizationUserRepository.GetManyAsync(organizationUserIds);
        var filteredUsers = orgUsers.Where(u => u.OrganizationId == organizationId)
            .ToList();

        if (!filteredUsers.Any())
        {
            throw new BadRequestException("Users invalid.");
        }

        if (!await HasConfirmedOwnersExceptAsync(organizationId, organizationUserIds))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }

        var deletingUserIsOwner = false;
        if (revokingUserId.HasValue)
        {
            deletingUserIsOwner = await _currentContext.OrganizationOwner(organizationId);
        }

        var result = new List<Tuple<OrganizationUser, string>>();

        foreach (var organizationUser in filteredUsers)
        {
            try
            {
                if (organizationUser.Status == OrganizationUserStatusType.Revoked)
                {
                    throw new BadRequestException("Already revoked.");
                }

                if (revokingUserId.HasValue && organizationUser.UserId == revokingUserId)
                {
                    throw new BadRequestException("You cannot revoke yourself.");
                }

                if (organizationUser.Type == OrganizationUserType.Owner && revokingUserId.HasValue && !deletingUserIsOwner)
                {
                    throw new BadRequestException("Only owners can revoke other owners.");
                }

                await _organizationUserRepository.RevokeAsync(organizationUser.Id);
                organizationUser.Status = OrganizationUserStatusType.Revoked;
                await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Revoked);

                result.Add(Tuple.Create(organizationUser, ""));
            }
            catch (BadRequestException e)
            {
                result.Add(Tuple.Create(organizationUser, e.Message));
            }
        }

        return result;
    }

    public async Task RestoreUserAsync(OrganizationUser organizationUser, Guid? restoringUserId,
        IUserService userService)
    {
        if (restoringUserId.HasValue && organizationUser.UserId == restoringUserId.Value)
        {
            throw new BadRequestException("You cannot restore yourself.");
        }

        if (organizationUser.Type == OrganizationUserType.Owner && restoringUserId.HasValue &&
            !await _currentContext.OrganizationOwner(organizationUser.OrganizationId))
        {
            throw new BadRequestException("Only owners can restore other owners.");
        }

        await RepositoryRestoreUserAsync(organizationUser, userService);
        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored);
    }

    public async Task RestoreUserAsync(OrganizationUser organizationUser, EventSystemUser systemUser,
        IUserService userService)
    {
        await RepositoryRestoreUserAsync(organizationUser, userService);
        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored, systemUser);
    }

    private async Task RepositoryRestoreUserAsync(OrganizationUser organizationUser, IUserService userService)
    {
        if (organizationUser.Status != OrganizationUserStatusType.Revoked)
        {
            throw new BadRequestException("Already active.");
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationUser.OrganizationId);
        var occupiedSeats = await _organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
        var availableSeats = organization.Seats.GetValueOrDefault(0) - occupiedSeats;
        if (availableSeats < 1)
        {
            await AutoAddSeatsAsync(organization, 1, DateTime.UtcNow);
        }

        await CheckPoliciesBeforeRestoreAsync(organizationUser, userService);

        var status = GetPriorActiveOrganizationUserStatusType(organizationUser);

        await _organizationUserRepository.RestoreAsync(organizationUser.Id, status);
        organizationUser.Status = status;
    }

    public async Task<List<Tuple<OrganizationUser, string>>> RestoreUsersAsync(Guid organizationId,
        IEnumerable<Guid> organizationUserIds, Guid? restoringUserId, IUserService userService)
    {
        var orgUsers = await _organizationUserRepository.GetManyAsync(organizationUserIds);
        var filteredUsers = orgUsers.Where(u => u.OrganizationId == organizationId)
            .ToList();

        if (!filteredUsers.Any())
        {
            throw new BadRequestException("Users invalid.");
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        var occupiedSeats = await _organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
        var availableSeats = organization.Seats.GetValueOrDefault(0) - occupiedSeats;
        var newSeatsRequired = organizationUserIds.Count() - availableSeats;
        await AutoAddSeatsAsync(organization, newSeatsRequired, DateTime.UtcNow);

        var deletingUserIsOwner = false;
        if (restoringUserId.HasValue)
        {
            deletingUserIsOwner = await _currentContext.OrganizationOwner(organizationId);
        }

        var result = new List<Tuple<OrganizationUser, string>>();

        foreach (var organizationUser in filteredUsers)
        {
            try
            {
                if (organizationUser.Status != OrganizationUserStatusType.Revoked)
                {
                    throw new BadRequestException("Already active.");
                }

                if (restoringUserId.HasValue && organizationUser.UserId == restoringUserId)
                {
                    throw new BadRequestException("You cannot restore yourself.");
                }

                if (organizationUser.Type == OrganizationUserType.Owner && restoringUserId.HasValue && !deletingUserIsOwner)
                {
                    throw new BadRequestException("Only owners can restore other owners.");
                }

                await CheckPoliciesBeforeRestoreAsync(organizationUser, userService);

                var status = GetPriorActiveOrganizationUserStatusType(organizationUser);

                await _organizationUserRepository.RestoreAsync(organizationUser.Id, status);
                organizationUser.Status = status;
                await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored);

                result.Add(Tuple.Create(organizationUser, ""));
            }
            catch (BadRequestException e)
            {
                result.Add(Tuple.Create(organizationUser, e.Message));
            }
        }

        return result;
    }

    private async Task CheckPoliciesBeforeRestoreAsync(OrganizationUser orgUser, IUserService userService)
    {
        // An invited OrganizationUser isn't linked with a user account yet, so these checks are irrelevant
        // The user will be subject to the same checks when they try to accept the invite
        if (GetPriorActiveOrganizationUserStatusType(orgUser) == OrganizationUserStatusType.Invited)
        {
            return;
        }

        var userId = orgUser.UserId.Value;

        // Enforce Single Organization Policy of organization user is being restored to
        var allOrgUsers = await _organizationUserRepository.GetManyByUserAsync(userId);
        var hasOtherOrgs = allOrgUsers.Any(ou => ou.OrganizationId != orgUser.OrganizationId);
        var singleOrgPoliciesApplyingToRevokedUsers = await _policyService.GetPoliciesApplicableToUserAsync(userId,
            PolicyType.SingleOrg, OrganizationUserStatusType.Revoked);
        var singleOrgPolicyApplies = singleOrgPoliciesApplyingToRevokedUsers.Any(p => p.OrganizationId == orgUser.OrganizationId);

        if (hasOtherOrgs && singleOrgPolicyApplies)
        {
            throw new BadRequestException("You cannot restore this user until " +
                "they leave or remove all other organizations.");
        }

        // Enforce Single Organization Policy of other organizations user is a member of
        var anySingleOrgPolicies = await _policyService.AnyPoliciesApplicableToUserAsync(userId,
            PolicyType.SingleOrg);
        if (anySingleOrgPolicies)
        {
            throw new BadRequestException("You cannot restore this user because they are a member of " +
                "another organization which forbids it");
        }

        // Enforce Two Factor Authentication Policy of organization user is trying to join
        var user = await _userRepository.GetByIdAsync(userId);
        if (!await userService.TwoFactorIsEnabledAsync(user))
        {
            var invitedTwoFactorPolicies = await _policyService.GetPoliciesApplicableToUserAsync(userId,
                PolicyType.TwoFactorAuthentication, OrganizationUserStatusType.Invited);
            if (invitedTwoFactorPolicies.Any(p => p.OrganizationId == orgUser.OrganizationId))
            {
                throw new BadRequestException("You cannot restore this user until they enable " +
                    "two-step login on their user account.");
            }
        }
    }

    static OrganizationUserStatusType GetPriorActiveOrganizationUserStatusType(OrganizationUser organizationUser)
    {
        // Determine status to revert back to
        var status = OrganizationUserStatusType.Invited;
        if (organizationUser.UserId.HasValue && string.IsNullOrWhiteSpace(organizationUser.Email))
        {
            // Has UserId & Email is null, then Accepted
            status = OrganizationUserStatusType.Accepted;
            if (!string.IsNullOrWhiteSpace(organizationUser.Key))
            {
                // We have an org key for this user, user was confirmed
                status = OrganizationUserStatusType.Confirmed;
            }
        }

        return status;
    }

    public async Task CreatePendingOrganization(Organization organization, string ownerEmail, ClaimsPrincipal user, IUserService userService, bool salesAssistedTrialStarted)
    {
        var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType);
        if (plan is not { LegacyYear: null })
        {
            throw new BadRequestException("Invalid plan selected.");
        }

        if (plan.Disabled)
        {
            throw new BadRequestException("Plan not found.");
        }

        organization.Id = CoreHelpers.GenerateComb();
        organization.Enabled = false;
        organization.Status = OrganizationStatusType.Pending;

        await SignUpAsync(organization, default, null, null, true);

        var ownerOrganizationUser = new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = null,
            Email = ownerEmail,
            Key = null,
            Type = OrganizationUserType.Owner,
            Status = OrganizationUserStatusType.Invited,
            AccessAll = true
        };
        await _organizationUserRepository.CreateAsync(ownerOrganizationUser);

        await SendInviteAsync(ownerOrganizationUser, organization, true);
        await _eventService.LogOrganizationUserEventAsync(ownerOrganizationUser, EventType.OrganizationUser_Invited);

        await _referenceEventService.RaiseEventAsync(new ReferenceEvent(ReferenceEventType.OrganizationCreatedByAdmin, organization, _currentContext)
        {
            EventRaisedByUser = userService.GetUserName(user),
            SalesAssistedTrialStarted = salesAssistedTrialStarted,
        });
    }

    public async Task InitPendingOrganization(Guid userId, Guid organizationId, string publicKey, string privateKey, string collectionName)
    {
        await ValidateSignUpPoliciesAsync(userId);

        var org = await GetOrgById(organizationId);

        if (org.Enabled)
        {
            throw new BadRequestException("Organization is already enabled.");
        }

        if (org.Status != OrganizationStatusType.Pending)
        {
            throw new BadRequestException("Organization is not on a Pending status.");
        }

        if (!string.IsNullOrEmpty(org.PublicKey))
        {
            throw new BadRequestException("Organization already has a Public Key.");
        }

        if (!string.IsNullOrEmpty(org.PrivateKey))
        {
            throw new BadRequestException("Organization already has a Private Key.");
        }

        org.Enabled = true;
        org.Status = OrganizationStatusType.Created;
        org.PublicKey = publicKey;
        org.PrivateKey = privateKey;

        await UpdateAsync(org);

        if (!string.IsNullOrWhiteSpace(collectionName))
        {
            var defaultCollection = new Collection
            {
                Name = collectionName,
                OrganizationId = org.Id
            };
            await _collectionRepository.CreateAsync(defaultCollection);
        }
    }
}
