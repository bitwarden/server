﻿using System.Security.Claims;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using Stripe;
using OrganizationUserInvite = Bit.Core.Models.Business.OrganizationUserInvite;

namespace Bit.Core.Services;

public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IMailService _mailService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IPushRegistrationService _pushRegistrationService;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ILicensingService _licensingService;
    private readonly IEventService _eventService;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IPaymentService _paymentService;
    private readonly IPolicyRepository _policyRepository;
    private readonly IPolicyService _policyService;
    private readonly ISsoUserRepository _ssoUserRepository;
    private readonly IGlobalSettings _globalSettings;
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;
    private readonly ICurrentContext _currentContext;
    private readonly ILogger<OrganizationService> _logger;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly ICountNewSmSeatsRequiredQuery _countNewSmSeatsRequiredQuery;
    private readonly IUpdateSecretsManagerSubscriptionCommand _updateSecretsManagerSubscriptionCommand;
    private readonly IProviderRepository _providerRepository;
    private readonly IFeatureService _featureService;
    private readonly IHasConfirmedOwnersExceptQuery _hasConfirmedOwnersExceptQuery;
    private readonly IPricingClient _pricingClient;
    private readonly IPolicyRequirementQuery _policyRequirementQuery;
    private readonly ISendOrganizationInvitesCommand _sendOrganizationInvitesCommand;

    public OrganizationService(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        IGroupRepository groupRepository,
        IMailService mailService,
        IPushNotificationService pushNotificationService,
        IPushRegistrationService pushRegistrationService,
        IDeviceRepository deviceRepository,
        ILicensingService licensingService,
        IEventService eventService,
        IApplicationCacheService applicationCacheService,
        IPaymentService paymentService,
        IPolicyRepository policyRepository,
        IPolicyService policyService,
        ISsoUserRepository ssoUserRepository,
        IGlobalSettings globalSettings,
        IOrganizationApiKeyRepository organizationApiKeyRepository,
        ICurrentContext currentContext,
        ILogger<OrganizationService> logger,
        IProviderOrganizationRepository providerOrganizationRepository,
        IProviderUserRepository providerUserRepository,
        ICountNewSmSeatsRequiredQuery countNewSmSeatsRequiredQuery,
        IUpdateSecretsManagerSubscriptionCommand updateSecretsManagerSubscriptionCommand,
        IProviderRepository providerRepository,
        IFeatureService featureService,
        IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery,
        IPricingClient pricingClient,
        IPolicyRequirementQuery policyRequirementQuery,
        ISendOrganizationInvitesCommand sendOrganizationInvitesCommand
        )
    {
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _collectionRepository = collectionRepository;
        _groupRepository = groupRepository;
        _mailService = mailService;
        _pushNotificationService = pushNotificationService;
        _pushRegistrationService = pushRegistrationService;
        _deviceRepository = deviceRepository;
        _licensingService = licensingService;
        _eventService = eventService;
        _applicationCacheService = applicationCacheService;
        _paymentService = paymentService;
        _policyRepository = policyRepository;
        _policyService = policyService;
        _ssoUserRepository = ssoUserRepository;
        _globalSettings = globalSettings;
        _organizationApiKeyRepository = organizationApiKeyRepository;
        _currentContext = currentContext;
        _logger = logger;
        _providerOrganizationRepository = providerOrganizationRepository;
        _providerUserRepository = providerUserRepository;
        _countNewSmSeatsRequiredQuery = countNewSmSeatsRequiredQuery;
        _updateSecretsManagerSubscriptionCommand = updateSecretsManagerSubscriptionCommand;
        _providerRepository = providerRepository;
        _featureService = featureService;
        _hasConfirmedOwnersExceptQuery = hasConfirmedOwnersExceptQuery;
        _pricingClient = pricingClient;
        _policyRequirementQuery = policyRequirementQuery;
        _sendOrganizationInvitesCommand = sendOrganizationInvitesCommand;
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
    }

    public async Task ReinstateSubscriptionAsync(Guid organizationId)
    {
        var organization = await GetOrgById(organizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        await _paymentService.ReinstateSubscriptionAsync(organization);
    }

    public async Task<string> AdjustStorageAsync(Guid organizationId, short storageAdjustmentGb)
    {
        var organization = await GetOrgById(organizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        var plan = await _pricingClient.GetPlanOrThrow(organization.PlanType);

        if (!plan.PasswordManager.HasAdditionalStorageOption)
        {
            throw new BadRequestException("Plan does not allow additional storage.");
        }

        var secret = await BillingHelpers.AdjustStorageAsync(_paymentService, organization, storageAdjustmentGb,
            plan.PasswordManager.StripeStoragePlanId);
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

        var plan = await _pricingClient.GetPlanOrThrow(organization.PlanType);
        if (plan == null)
        {
            throw new BadRequestException("Existing plan not found.");
        }

        if (!plan.PasswordManager.AllowSeatAutoscale)
        {
            throw new BadRequestException("Your plan does not allow seat autoscaling.");
        }

        if (plan.PasswordManager.MaxSeats.HasValue && maxAutoscaleSeats.HasValue &&
            maxAutoscaleSeats > plan.PasswordManager.MaxSeats)
        {
            throw new BadRequestException(string.Concat($"Your plan has a seat limit of {plan.PasswordManager.MaxSeats}, ",
                $"but you have specified a max autoscale count of {maxAutoscaleSeats}.",
                "Reduce your max autoscale seat count."));
        }

        organization.MaxAutoscaleSeats = maxAutoscaleSeats;

        await ReplaceAndUpdateCacheAsync(organization);
    }

    public async Task<string> AdjustSeatsAsync(Guid organizationId, int seatAdjustment)
    {
        var organization = await GetOrgById(organizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        return await AdjustSeatsAsync(organization, seatAdjustment);
    }

    private async Task<string> AdjustSeatsAsync(Organization organization, int seatAdjustment, IEnumerable<string> ownerEmails = null)
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

        var plan = await _pricingClient.GetPlanOrThrow(organization.PlanType);

        if (!plan.PasswordManager.HasAdditionalSeatsOption)
        {
            throw new BadRequestException("Plan does not allow additional seats.");
        }

        var newSeatTotal = organization.Seats.Value + seatAdjustment;
        if (plan.PasswordManager.BaseSeats > newSeatTotal)
        {
            throw new BadRequestException($"Plan has a minimum of {plan.PasswordManager.BaseSeats} seats.");
        }

        if (newSeatTotal <= 0)
        {
            throw new BadRequestException("You must have at least 1 seat.");
        }

        var additionalSeats = newSeatTotal - plan.PasswordManager.BaseSeats;
        if (plan.PasswordManager.MaxAdditionalSeats.HasValue && additionalSeats > plan.PasswordManager.MaxAdditionalSeats.Value)
        {
            throw new BadRequestException($"Organization plan allows a maximum of " +
                $"{plan.PasswordManager.MaxAdditionalSeats.Value} additional seats.");
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

        if (organization.UseSecretsManager && organization.Seats + seatAdjustment < organization.SmSeats)
        {
            throw new BadRequestException("You cannot have more Secrets Manager seats than Password Manager seats.");
        }

        var paymentIntentClientSecret = await _paymentService.AdjustSeatsAsync(organization, plan, additionalSeats);
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

    private async Task ValidateSignUpPoliciesAsync(Guid ownerId)
    {
        var anySingleOrgPolicies = await _policyService.AnyPoliciesApplicableToUserAsync(ownerId, PolicyType.SingleOrg);
        if (anySingleOrgPolicies)
        {
            throw new BadRequestException("You may not create an organization. You belong to an organization " +
                "which has a policy that prohibits you from being a member of any other organization.");
        }
    }

    /// <summary>
    /// Create a new organization on a self-hosted instance
    /// </summary>
    public async Task<(Organization organization, OrganizationUser organizationUser)> SignUpAsync(
        OrganizationLicense license, User owner, string ownerKey, string collectionName, string publicKey,
        string privateKey)
    {
        if (license.LicenseType != LicenseType.Organization)
        {
            throw new BadRequestException("Premium licenses cannot be applied to an organization. " +
                "Upload this license from your personal account settings page.");
        }

        var claimsPrincipal = _licensingService.GetClaimsPrincipalFromLicense(license);
        var canUse = license.CanUse(_globalSettings, _licensingService, claimsPrincipal, out var exception);

        if (!canUse)
        {
            throw new BadRequestException(exception);
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
            Status = OrganizationStatusType.Created,
            UsePasswordManager = license.UsePasswordManager,
            UseSecretsManager = license.UseSecretsManager,
            SmSeats = license.SmSeats,
            SmServiceAccounts = license.SmServiceAccounts,
            UseRiskInsights = license.UseRiskInsights,
            UseOrganizationDomains = license.UseOrganizationDomains,
            UseAdminSponsoredFamilies = license.UseAdminSponsoredFamilies,
        };

        var result = await SignUpAsync(organization, owner.Id, ownerKey, collectionName, false);

        var dir = $"{_globalSettings.LicenseDirectory}/organization";
        Directory.CreateDirectory(dir);
        await using var fs = new FileStream(Path.Combine(dir, $"{organization.Id}.json"), FileMode.Create);
        await JsonSerializer.SerializeAsync(fs, license, JsonHelpers.Indented);
        return (result.organization, result.organizationUser);
    }

    /// <summary>
    /// Private helper method to create a new organization.
    /// This is common code used by both the cloud and self-hosted methods.
    /// </summary>
    private async Task<(Organization organization, OrganizationUser organizationUser, Collection defaultCollection)> SignUpAsync(Organization organization,
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

            // ownerId == default if the org is created by a provider - in this case it's created without an
            // owner and the first owner is immediately invited afterwards
            OrganizationUser orgUser = null;
            if (ownerId != default)
            {
                orgUser = new OrganizationUser
                {
                    OrganizationId = organization.Id,
                    UserId = ownerId,
                    Key = ownerKey,
                    AccessSecretsManager = organization.UseSecretsManager,
                    Type = OrganizationUserType.Owner,
                    Status = OrganizationUserStatusType.Confirmed,
                    CreationDate = organization.CreationDate,
                    RevisionDate = organization.CreationDate
                };
                orgUser.SetNewId();

                await _organizationUserRepository.CreateAsync(orgUser);

                var devices = await GetUserDeviceIdsAsync(orgUser.UserId.Value);
                await _pushRegistrationService.AddUserRegistrationOrganizationAsync(devices,
                    organization.Id.ToString());
                await _pushNotificationService.PushSyncOrgKeysAsync(ownerId);
            }

            Collection defaultCollection = null;
            if (!string.IsNullOrWhiteSpace(collectionName))
            {
                defaultCollection = new Collection
                {
                    Name = collectionName,
                    OrganizationId = organization.Id,
                    CreationDate = organization.CreationDate,
                    RevisionDate = organization.CreationDate
                };

                // Give the owner Can Manage access over the default collection
                List<CollectionAccessSelection> defaultOwnerAccess = null;
                if (orgUser != null)
                {
                    defaultOwnerAccess =
                        [new CollectionAccessSelection { Id = orgUser.Id, HidePasswords = false, ReadOnly = false, Manage = true }];
                }

                await _collectionRepository.CreateAsync(defaultCollection, null, defaultOwnerAccess);
            }

            return (organization, orgUser, defaultCollection);
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

    public async Task UpdateAsync(Organization organization, bool updateBilling = false, EventType eventType = EventType.Organization_Updated)
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

        await ReplaceAndUpdateCacheAsync(organization, eventType);

        if (updateBilling && !string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
        {
            var customerService = new CustomerService();
            await customerService.UpdateAsync(organization.GatewayCustomerId, new CustomerUpdateOptions
            {
                Email = organization.BillingEmail,
                Description = organization.DisplayBusinessName()
            });
        }

        if (eventType == EventType.Organization_CollectionManagement_Updated)
        {
            await _pushNotificationService.PushSyncOrganizationCollectionManagementSettingsAsync(organization);
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

    public async Task<OrganizationUser> InviteUserAsync(Guid organizationId, Guid? invitingUserId, EventSystemUser? systemUser,
        OrganizationUserInvite invite, string externalId)
    {
        // Ideally OrganizationUserInvite should represent a single user so that this doesn't have to be a runtime check
        if (invite.Emails.Count() > 1)
        {
            throw new BadRequestException("This method can only be used to invite a single user.");
        }

        // Validate Collection associations
        var invalidAssociations = invite.Collections?.Where(cas => cas.Manage && (cas.ReadOnly || cas.HidePasswords));
        if (invalidAssociations?.Any() ?? false)
        {
            throw new BadRequestException("The Manage property is mutually exclusive and cannot be true while the ReadOnly or HidePasswords properties are also true.");
        }

        var results = await InviteUsersAsync(organizationId, invitingUserId, systemUser,
            new (OrganizationUserInvite, string)[] { (invite, externalId) });

        var result = results.FirstOrDefault();
        if (result == null)
        {
            throw new BadRequestException("This user has already been invited.");
        }
        return result;
    }

    /// <summary>
    /// Invite users to an organization.
    /// </summary>
    /// <param name="organizationId">The organization Id</param>
    /// <param name="invitingUserId">The current authenticated user who is sending the invite. Only used when inviting via a client app; null if using SCIM or Public API.</param>
    /// <param name="systemUser">The system user which is sending the invite. Only used when inviting via SCIM; null if using a client app or Public API</param>
    /// <param name="invites">Details about the users being invited</param>
    /// <returns></returns>
    public async Task<List<OrganizationUser>> InviteUsersAsync(Guid organizationId, Guid? invitingUserId, EventSystemUser? systemUser,
        IEnumerable<(OrganizationUserInvite invite, string externalId)> invites)
    {
        var inviteTypes = new HashSet<OrganizationUserType>(invites.Where(i => i.invite.Type.HasValue)
            .Select(i => i.invite.Type.Value));

        // If authenticating via a client app, verify the inviting user has permissions
        // cf. SCIM and Public API have superuser permissions here
        if (invitingUserId.HasValue && inviteTypes.Count > 0)
        {
            foreach (var (invite, _) in invites)
            {
                await ValidateOrganizationUserUpdatePermissions(organizationId, invite.Type.Value, null, invite.Permissions);
                await ValidateOrganizationCustomPermissionsEnabledAsync(organizationId, invite.Type.Value);
            }
        }

        var (organizationUsers, events) = await SaveUsersSendInvitesAsync(organizationId, invites);

        if (systemUser.HasValue)
        {
            // Log SCIM event
            await _eventService.LogOrganizationUserEventsAsync(events.Select(e => (e.Item1, e.Item2, systemUser.Value, e.Item3)));
        }
        else
        {
            // Log client app or Public Api event
            await _eventService.LogOrganizationUserEventsAsync(events);
        }

        return organizationUsers;
    }

    private async Task<(List<OrganizationUser> organizationUsers, List<(OrganizationUser, EventType, DateTime?)> events)> SaveUsersSendInvitesAsync(Guid organizationId,
        IEnumerable<(OrganizationUserInvite invite, string externalId)> invites)
    {
        var organization = await GetOrgById(organizationId);
        var initialSeatCount = organization.Seats;
        if (organization == null || invites.Any(i => i.invite.Emails == null))
        {
            throw new NotFoundException();
        }

        var existingEmails = new HashSet<string>(await _organizationUserRepository.SelectKnownEmailsAsync(
            organizationId, invites.SelectMany(i => i.invite.Emails), false), StringComparer.InvariantCultureIgnoreCase);

        // Seat autoscaling
        var initialSmSeatCount = organization.SmSeats;
        var newSeatsRequired = 0;
        if (organization.Seats.HasValue)
        {
            var occupiedSeats = await _organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
            var availableSeats = organization.Seats.Value - occupiedSeats;
            newSeatsRequired = invites.Sum(i => i.invite.Emails.Count()) - existingEmails.Count() - availableSeats;
        }

        if (newSeatsRequired > 0)
        {
            var (canScale, failureReason) = await CanScaleAsync(organization, newSeatsRequired);
            if (!canScale)
            {
                throw new BadRequestException(failureReason);
            }
        }

        // Secrets Manager seat autoscaling
        SecretsManagerSubscriptionUpdate smSubscriptionUpdate = null;
        var inviteWithSmAccessCount = invites
            .Where(i => i.invite.AccessSecretsManager)
            .SelectMany(i => i.invite.Emails)
            .Count(email => !existingEmails.Contains(email));

        var additionalSmSeatsRequired = await _countNewSmSeatsRequiredQuery.CountNewSmSeatsRequiredAsync(organization.Id, inviteWithSmAccessCount);
        if (additionalSmSeatsRequired > 0)
        {
            var plan = await _pricingClient.GetPlanOrThrow(organization.PlanType);
            smSubscriptionUpdate = new SecretsManagerSubscriptionUpdate(organization, plan, true)
                .AdjustSeats(additionalSmSeatsRequired);
        }

        var invitedAreAllOwners = invites.All(i => i.invite.Type == OrganizationUserType.Owner);
        if (!invitedAreAllOwners && !await _hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(organizationId, new Guid[] { }, includeProvider: true))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }

        var orgUsersWithoutCollections = new List<OrganizationUser>();
        var orgUsersWithCollections = new List<(OrganizationUser, IEnumerable<CollectionAccessSelection>)>();
        var orgUserGroups = new List<(OrganizationUser, IEnumerable<Guid>)>();
        var orgUserInvitedCount = 0;
        var exceptions = new List<Exception>();
        var events = new List<(OrganizationUser, EventType, DateTime?)>();
        foreach (var (invite, externalId) in invites)
        {
            // Prevent duplicate invitations
            foreach (var email in invite.Emails.Distinct())
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
                        AccessSecretsManager = invite.AccessSecretsManager,
                        ExternalId = externalId,
                        CreationDate = DateTime.UtcNow,
                        RevisionDate = DateTime.UtcNow,
                    };

                    if (invite.Type == OrganizationUserType.Custom)
                    {
                        orgUser.SetPermissions(invite.Permissions ?? new Permissions());
                    }

                    if (invite.Collections.Any())
                    {
                        orgUsersWithCollections.Add((orgUser, invite.Collections));
                    }
                    else
                    {
                        orgUsersWithoutCollections.Add(orgUser);
                    }

                    if (invite.Groups != null && invite.Groups.Any())
                    {
                        orgUserGroups.Add((orgUser, invite.Groups));
                    }

                    events.Add((orgUser, EventType.OrganizationUser_Invited, DateTime.UtcNow));
                    orgUserInvitedCount++;
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }
        }

        if (exceptions.Any())
        {
            throw new AggregateException("One or more errors occurred while inviting users.", exceptions);
        }

        var allOrgUsers = orgUsersWithoutCollections
            .Concat(orgUsersWithCollections.Select(u => u.Item1))
            .ToList();

        try
        {
            await _organizationUserRepository.CreateManyAsync(orgUsersWithoutCollections);
            foreach (var (orgUser, collections) in orgUsersWithCollections)
            {
                await _organizationUserRepository.CreateAsync(orgUser, collections);
            }

            foreach (var (orgUser, groups) in orgUserGroups)
            {
                await _organizationUserRepository.UpdateGroupsAsync(orgUser.Id, groups);
            }

            if (!await _currentContext.ManageUsers(organization.Id))
            {
                throw new BadRequestException("Cannot add seats. Cannot manage organization users.");
            }

            await AutoAddSeatsAsync(organization, newSeatsRequired);

            if (additionalSmSeatsRequired > 0)
            {
                await _updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(smSubscriptionUpdate);
            }

            await SendInvitesAsync(allOrgUsers, organization);
        }
        catch (Exception e)
        {
            // Revert any added users.
            var invitedOrgUserIds = allOrgUsers.Select(ou => ou.Id);
            await _organizationUserRepository.DeleteManyAsync(invitedOrgUserIds);
            var currentOrganization = await _organizationRepository.GetByIdAsync(organization.Id);

            // Revert autoscaling
            // Do this first so that SmSeats never exceed PM seats (due to current billing requirements)
            if (initialSmSeatCount.HasValue && currentOrganization.SmSeats.HasValue &&
                currentOrganization.SmSeats.Value != initialSmSeatCount.Value)
            {
                var plan = await _pricingClient.GetPlanOrThrow(currentOrganization.PlanType);
                var smSubscriptionUpdateRevert = new SecretsManagerSubscriptionUpdate(currentOrganization, plan, false)
                {
                    SmSeats = initialSmSeatCount.Value
                };
                await _updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(smSubscriptionUpdateRevert);
            }

            if (initialSeatCount.HasValue && currentOrganization.Seats.HasValue && currentOrganization.Seats.Value != initialSeatCount.Value)
            {
                await AdjustSeatsAsync(organization, initialSeatCount.Value - currentOrganization.Seats.Value);
            }

            exceptions.Add(e);
        }

        if (exceptions.Any())
        {
            throw new AggregateException("One or more errors occurred while inviting users.", exceptions);
        }

        return (allOrgUsers, events);
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

    private async Task SendInvitesAsync(IEnumerable<OrganizationUser> orgUsers, Organization organization) =>
        await _sendOrganizationInvitesCommand.SendInvitesAsync(new SendInvitesRequest(orgUsers, organization));

    private async Task SendInviteAsync(OrganizationUser orgUser, Organization organization, bool initOrganization) =>
        await _sendOrganizationInvitesCommand.SendInvitesAsync(new SendInvitesRequest(
            users: [orgUser],
            organization: organization,
            initOrganization: initOrganization));

    public async Task<(bool canScale, string failureReason)> CanScaleAsync(
        Organization organization,
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

        var provider = await _providerRepository.GetByOrganizationIdAsync(organization.Id);

        if (provider is { Enabled: true })
        {
            if (provider.IsBillable())
            {
                return (false, "Seat limit has been reached. Please contact your provider to add more seats.");
            }

            if (provider.Type == ProviderType.Reseller)
            {
                return (false, "Seat limit has been reached. Contact your provider to purchase additional seats.");
            }
        }

        var subscription = await _paymentService.GetSubscriptionAsync(organization);
        if (subscription?.Subscription?.Status == StripeConstants.SubscriptionStatus.Canceled)
        {
            return (false, "You do not have an active subscription. Reinstate your subscription to make changes");
        }

        if (organization.Seats.HasValue &&
            organization.MaxAutoscaleSeats.HasValue &&
            organization.MaxAutoscaleSeats.Value < organization.Seats.Value + seatsToAdd)
        {
            return (false, $"Seat limit has been reached.");
        }

        return (true, failureReason);
    }

    public async Task AutoAddSeatsAsync(Organization organization, int seatsToAdd)
    {
        if (seatsToAdd < 1 || !organization.Seats.HasValue)
        {
            return;
        }

        var (canScale, failureMessage) = await CanScaleAsync(organization, seatsToAdd);
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

        await AdjustSeatsAsync(organization, seatsToAdd, ownerEmails);

        if (!organization.OwnersNotifiedOfAutoscaling.HasValue)
        {
            await _mailService.SendOrganizationAutoscaledEmailAsync(organization, initialSeatCount,
                ownerEmails);
            organization.OwnersNotifiedOfAutoscaling = DateTime.UtcNow;
            await _organizationRepository.UpsertAsync(organization);
        }
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
        if (_featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements))
        {
            var resetPasswordPolicyRequirement = await _policyRequirementQuery.GetAsync<ResetPasswordPolicyRequirement>(userId);
            if (resetPasswordKey == null && resetPasswordPolicyRequirement.AutoEnrollEnabled(organizationId))
            {
                throw new BadRequestException("Due to an Enterprise Policy, you are not allowed to withdraw from account recovery.");
            }

        }
        else
        {
            if (resetPasswordKey == null && resetPasswordPolicy.Data != null)
            {
                var data = JsonSerializer.Deserialize<ResetPasswordDataModel>(resetPasswordPolicy.Data, JsonHelpers.IgnoreCase);

                if (data?.AutoEnrollEnabled ?? false)
                {
                    throw new BadRequestException("Due to an Enterprise Policy, you are not allowed to withdraw from account recovery.");
                }
            }
        }

        orgUser.ResetPasswordKey = resetPasswordKey;
        await _organizationUserRepository.ReplaceAsync(orgUser);
        await _eventService.LogOrganizationUserEventAsync(orgUser, resetPasswordKey != null ?
            EventType.OrganizationUser_ResetPassword_Enroll : EventType.OrganizationUser_ResetPassword_Withdraw);
    }

    public async Task ImportAsync(Guid organizationId,
        IEnumerable<ImportedGroup> groups,
        IEnumerable<ImportedOrganizationUser> newUsers,
        IEnumerable<string> removeUserExternalIds,
        bool overwriteExisting,
        EventSystemUser eventSystemUser
    )
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

        var events = new List<(OrganizationUserUserDetails ou, EventType e, DateTime? d)>();

        // Remove Users
        if (removeUserExternalIds?.Any() ?? false)
        {
            var existingUsersDict = existingExternalUsers.ToDictionary(u => u.ExternalId);
            var removeUsersSet = new HashSet<string>(removeUserExternalIds)
                .Except(newUsersSet)
                .Where(u => existingUsersDict.ContainsKey(u) && existingUsersDict[u].Type != OrganizationUserType.Owner)
                .Select(u => existingUsersDict[u]);

            await _organizationUserRepository.DeleteManyAsync(removeUsersSet.Select(u => u.Id));
            events.AddRange(removeUsersSet.Select(u => (
              u,
              EventType.OrganizationUser_Removed,
              (DateTime?)DateTime.UtcNow
              ))
            );
        }

        if (overwriteExisting)
        {
            // Remove existing external users that are not in new user set
            var usersToDelete = existingExternalUsers.Where(u =>
                u.Type != OrganizationUserType.Owner &&
                !newUsersSet.Contains(u.ExternalId) &&
                existingExternalUsersIdDict.ContainsKey(u.ExternalId));
            await _organizationUserRepository.DeleteManyAsync(usersToDelete.Select(u => u.Id));
            events.AddRange(usersToDelete.Select(u => (
              u,
              EventType.OrganizationUser_Removed,
              (DateTime?)DateTime.UtcNow
              ))
            );
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
                var occupiedSeats = await _organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
                seatsAvailable = organization.Seats.Value - occupiedSeats;
                enoughSeatsAvailable = seatsAvailable >= usersToAdd.Count;
            }

            var hasStandaloneSecretsManager = await _paymentService.HasSecretsManagerStandalone(organization);

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
                        Collections = new List<CollectionAccessSelection>(),
                        AccessSecretsManager = hasStandaloneSecretsManager
                    };
                    userInvites.Add((invite, user.ExternalId));
                }
                catch (BadRequestException)
                {
                    // Thrown when the user is already invited to the organization
                    continue;
                }
            }

            var invitedUsers = await InviteUsersAsync(organizationId, invitingUserId: null, systemUser: eventSystemUser, userInvites);
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
                .Select(g => g.Group).ToList();

            var savedGroups = new List<Group>();
            foreach (var group in newGroups)
            {
                group.CreationDate = group.RevisionDate = DateTime.UtcNow;

                savedGroups.Add(await _groupRepository.CreateAsync(group));
                await UpdateUsersAsync(group, groupsDict[group.ExternalId].ExternalUserIds,
                    existingExternalUsersIdDict);
            }

            await _eventService.LogGroupEventsAsync(
                savedGroups.Select(g => (g, EventType.Group_Created, (EventSystemUser?)eventSystemUser, (DateTime?)DateTime.UtcNow)));

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

                await _eventService.LogGroupEventsAsync(
                    updateGroups.Select(g => (g, EventType.Group_Updated, (EventSystemUser?)eventSystemUser, (DateTime?)DateTime.UtcNow)));
            }
        }

        await _eventService.LogOrganizationUserEventsAsync(events.Select(e => (e.ou, e.e, eventSystemUser, e.d)));
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

    private async Task<IEnumerable<string>> GetUserDeviceIdsAsync(Guid userId)
    {
        var devices = await _deviceRepository.GetManyByUserIdAsync(userId);
        return devices
            .Where(d => !string.IsNullOrWhiteSpace(d.PushToken))
            .Select(d => d.Id.ToString());
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

    private static void ValidatePlan(Models.StaticStore.Plan plan, int additionalSeats, string productType)
    {
        if (plan is null)
        {
            throw new BadRequestException($"{productType} Plan was null.");
        }

        if (plan.Disabled)
        {
            throw new BadRequestException($"{productType} Plan not found.");
        }

        if (additionalSeats < 0)
        {
            throw new BadRequestException($"You can't subtract {productType} seats!");
        }
    }

    public void ValidatePasswordManagerPlan(Models.StaticStore.Plan plan, OrganizationUpgrade upgrade)
    {
        ValidatePlan(plan, upgrade.AdditionalSeats, "Password Manager");

        if (plan.PasswordManager.BaseSeats + upgrade.AdditionalSeats <= 0)
        {
            throw new BadRequestException($"You do not have any Password Manager seats!");
        }

        if (upgrade.AdditionalSeats < 0)
        {
            throw new BadRequestException($"You can't subtract Password Manager seats!");
        }

        if (!plan.PasswordManager.HasAdditionalStorageOption && upgrade.AdditionalStorageGb > 0)
        {
            throw new BadRequestException("Plan does not allow additional storage.");
        }

        if (upgrade.AdditionalStorageGb < 0)
        {
            throw new BadRequestException("You can't subtract storage!");
        }

        if (!plan.PasswordManager.HasPremiumAccessOption && upgrade.PremiumAccessAddon)
        {
            throw new BadRequestException("This plan does not allow you to buy the premium access addon.");
        }

        if (!plan.PasswordManager.HasAdditionalSeatsOption && upgrade.AdditionalSeats > 0)
        {
            throw new BadRequestException("Plan does not allow additional users.");
        }

        if (plan.PasswordManager.HasAdditionalSeatsOption && plan.PasswordManager.MaxAdditionalSeats.HasValue &&
            upgrade.AdditionalSeats > plan.PasswordManager.MaxAdditionalSeats.Value)
        {
            throw new BadRequestException($"Selected plan allows a maximum of " +
                                          $"{plan.PasswordManager.MaxAdditionalSeats.GetValueOrDefault(0)} additional users.");
        }
    }

    public void ValidateSecretsManagerPlan(Models.StaticStore.Plan plan, OrganizationUpgrade upgrade)
    {
        if (plan.SupportsSecretsManager == false)
        {
            throw new BadRequestException("Invalid Secrets Manager plan selected.");
        }

        ValidatePlan(plan, upgrade.AdditionalSmSeats.GetValueOrDefault(), "Secrets Manager");

        if (plan.SecretsManager.BaseSeats + upgrade.AdditionalSmSeats <= 0)
        {
            throw new BadRequestException($"You do not have any Secrets Manager seats!");
        }

        if (!plan.SecretsManager.HasAdditionalServiceAccountOption && upgrade.AdditionalServiceAccounts > 0)
        {
            throw new BadRequestException("Plan does not allow additional Machine Accounts.");
        }

        if ((plan.ProductTier == ProductTierType.TeamsStarter &&
            upgrade.AdditionalSmSeats.GetValueOrDefault() > plan.PasswordManager.BaseSeats) ||
            (plan.ProductTier != ProductTierType.TeamsStarter &&
             upgrade.AdditionalSmSeats.GetValueOrDefault() > upgrade.AdditionalSeats))
        {
            throw new BadRequestException("You cannot have more Secrets Manager seats than Password Manager seats.");
        }

        if (upgrade.AdditionalServiceAccounts.GetValueOrDefault() < 0)
        {
            throw new BadRequestException("You can't subtract Machine Accounts!");
        }

        switch (plan.SecretsManager.HasAdditionalSeatsOption)
        {
            case false when upgrade.AdditionalSmSeats > 0:
                throw new BadRequestException("Plan does not allow additional users.");
            case true when plan.SecretsManager.MaxAdditionalSeats.HasValue &&
                           upgrade.AdditionalSmSeats > plan.SecretsManager.MaxAdditionalSeats.Value:
                throw new BadRequestException($"Selected plan allows a maximum of " +
                                              $"{plan.SecretsManager.MaxAdditionalSeats.GetValueOrDefault(0)} additional users.");
        }
    }

    public async Task ValidateOrganizationUserUpdatePermissions(Guid organizationId, OrganizationUserType newType, OrganizationUserType? oldType, Permissions permissions)
    {
        if (await _currentContext.OrganizationOwner(organizationId))
        {
            return;
        }

        if (oldType == OrganizationUserType.Owner || newType == OrganizationUserType.Owner)
        {
            throw new BadRequestException("Only an Owner can configure another Owner's account.");
        }

        if (await _currentContext.OrganizationAdmin(organizationId))
        {
            return;
        }

        if (!await _currentContext.ManageUsers(organizationId))
        {
            throw new BadRequestException("Your account does not have permission to manage users.");
        }

        if (oldType == OrganizationUserType.Admin || newType == OrganizationUserType.Admin)
        {
            throw new BadRequestException("Custom users can not manage Admins or Owners.");
        }

        if (newType == OrganizationUserType.Custom && !await ValidateCustomPermissionsGrant(organizationId, permissions))
        {
            throw new BadRequestException("Custom users can only grant the same custom permissions that they have.");
        }
    }

    public async Task ValidateOrganizationCustomPermissionsEnabledAsync(Guid organizationId, OrganizationUserType newType)
    {
        if (newType != OrganizationUserType.Custom)
        {
            return;
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (!organization.UseCustomPermissions)
        {
            throw new BadRequestException("To enable custom permissions the organization must be on an Enterprise plan.");
        }
    }

    private async Task<bool> ValidateCustomPermissionsGrant(Guid organizationId, Permissions permissions)
    {
        if (permissions == null || await _currentContext.OrganizationAdmin(organizationId))
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

        if (permissions.EditAnyCollection && !await _currentContext.EditAnyCollection(organizationId))
        {
            return false;
        }

        if (permissions.ManageResetPassword && !await _currentContext.ManageResetPassword(organizationId))
        {
            return false;
        }

        var org = _currentContext.GetOrganization(organizationId);
        if (org == null)
        {
            return false;
        }

        if (permissions.CreateNewCollections && !org.Permissions.CreateNewCollections)
        {
            return false;
        }

        if (permissions.DeleteAnyCollection && !org.Permissions.DeleteAnyCollection)
        {
            return false;
        }

        return true;
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

        if (organizationUser.UserId.HasValue)
        {
            await _pushNotificationService.PushSyncOrgKeysAsync(organizationUser.UserId.Value);
        }
    }

    public async Task RevokeUserAsync(OrganizationUser organizationUser,
        EventSystemUser systemUser)
    {
        await RepositoryRevokeUserAsync(organizationUser);
        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Revoked, systemUser);

        if (organizationUser.UserId.HasValue)
        {
            await _pushNotificationService.PushSyncOrgKeysAsync(organizationUser.UserId.Value);
        }
    }

    private async Task RepositoryRevokeUserAsync(OrganizationUser organizationUser)
    {
        if (organizationUser.Status == OrganizationUserStatusType.Revoked)
        {
            throw new BadRequestException("Already revoked.");
        }

        if (!await _hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(organizationUser.OrganizationId, new[] { organizationUser.Id }, includeProvider: true))
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

        if (!await _hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(organizationId, organizationUserIds))
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
                if (organizationUser.UserId.HasValue)
                {
                    await _pushNotificationService.PushSyncOrgKeysAsync(organizationUser.UserId.Value);
                }

                result.Add(Tuple.Create(organizationUser, ""));
            }
            catch (BadRequestException e)
            {
                result.Add(Tuple.Create(organizationUser, e.Message));
            }
        }

        return result;
    }

    public static OrganizationUserStatusType GetPriorActiveOrganizationUserStatusType(OrganizationUser organizationUser)
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
        };
        await _organizationUserRepository.CreateAsync(ownerOrganizationUser);

        await SendInviteAsync(ownerOrganizationUser, organization, true);
        await _eventService.LogOrganizationUserEventAsync(ownerOrganizationUser, EventType.OrganizationUser_Invited);
    }
}
