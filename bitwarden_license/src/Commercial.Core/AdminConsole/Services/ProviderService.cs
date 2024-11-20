using System.ComponentModel.DataAnnotations;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Business.Provider;
using Bit.Core.AdminConsole.Models.Business.Tokenables;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Stripe;

namespace Bit.Commercial.Core.AdminConsole.Services;

public class ProviderService : IProviderService
{
    public static PlanType[] ProviderDisallowedOrganizationTypes = new[] { PlanType.Free, PlanType.FamiliesAnnually, PlanType.FamiliesAnnually2019 };

    private readonly IDataProtector _dataProtector;
    private readonly IMailService _mailService;
    private readonly IEventService _eventService;
    private readonly GlobalSettings _globalSettings;
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly IOrganizationService _organizationService;
    private readonly ICurrentContext _currentContext;
    private readonly IStripeAdapter _stripeAdapter;
    private readonly IFeatureService _featureService;
    private readonly IDataProtectorTokenFactory<ProviderDeleteTokenable> _providerDeleteTokenDataFactory;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IProviderBillingService _providerBillingService;

    public ProviderService(IProviderRepository providerRepository, IProviderUserRepository providerUserRepository,
        IProviderOrganizationRepository providerOrganizationRepository, IUserRepository userRepository,
        IUserService userService, IOrganizationService organizationService, IMailService mailService,
        IDataProtectionProvider dataProtectionProvider, IEventService eventService,
        IOrganizationRepository organizationRepository, GlobalSettings globalSettings,
        ICurrentContext currentContext, IStripeAdapter stripeAdapter, IFeatureService featureService,
        IDataProtectorTokenFactory<ProviderDeleteTokenable> providerDeleteTokenDataFactory,
        IApplicationCacheService applicationCacheService, IProviderBillingService providerBillingService)
    {
        _providerRepository = providerRepository;
        _providerUserRepository = providerUserRepository;
        _providerOrganizationRepository = providerOrganizationRepository;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _userService = userService;
        _organizationService = organizationService;
        _mailService = mailService;
        _eventService = eventService;
        _globalSettings = globalSettings;
        _dataProtector = dataProtectionProvider.CreateProtector("ProviderServiceDataProtector");
        _currentContext = currentContext;
        _stripeAdapter = stripeAdapter;
        _featureService = featureService;
        _providerDeleteTokenDataFactory = providerDeleteTokenDataFactory;
        _applicationCacheService = applicationCacheService;
        _providerBillingService = providerBillingService;
    }

    public async Task<Provider> CompleteSetupAsync(Provider provider, Guid ownerUserId, string token, string key, TaxInfo taxInfo = null)
    {
        var owner = await _userService.GetUserByIdAsync(ownerUserId);
        if (owner == null)
        {
            throw new BadRequestException("Invalid owner.");
        }

        if (provider.Status != ProviderStatusType.Pending)
        {
            throw new BadRequestException("Provider is already setup.");
        }

        if (!CoreHelpers.TokenIsValid("ProviderSetupInvite", _dataProtector, token, owner.Email, provider.Id,
            _globalSettings.OrganizationInviteExpirationHours))
        {
            throw new BadRequestException("Invalid token.");
        }

        var providerUser = await _providerUserRepository.GetByProviderUserAsync(provider.Id, ownerUserId);
        if (!(providerUser is { Type: ProviderUserType.ProviderAdmin }))
        {
            throw new BadRequestException("Invalid owner.");
        }

        if (taxInfo == null || string.IsNullOrEmpty(taxInfo.BillingAddressCountry) || string.IsNullOrEmpty(taxInfo.BillingAddressPostalCode))
        {
            throw new BadRequestException("Both address and postal code are required to set up your provider.");
        }
        var customer = await _providerBillingService.SetupCustomer(provider, taxInfo);
        provider.GatewayCustomerId = customer.Id;
        var subscription = await _providerBillingService.SetupSubscription(provider);
        provider.GatewaySubscriptionId = subscription.Id;
        provider.Status = ProviderStatusType.Billable;
        await _providerRepository.UpsertAsync(provider);

        providerUser.Key = key;
        await _providerUserRepository.ReplaceAsync(providerUser);

        return provider;
    }

    public async Task UpdateAsync(Provider provider, bool updateBilling = false)
    {
        if (provider.Id == default)
        {
            throw new ArgumentException("Cannot create provider this way.");
        }

        await _providerRepository.ReplaceAsync(provider);
    }

    public async Task<List<ProviderUser>> InviteUserAsync(ProviderUserInvite<string> invite)
    {
        if (!_currentContext.ProviderManageUsers(invite.ProviderId))
        {
            throw new InvalidOperationException("Invalid permissions.");
        }

        var emails = invite?.UserIdentifiers;
        var invitingUser = await _providerUserRepository.GetByProviderUserAsync(invite.ProviderId, invite.InvitingUserId);

        var provider = await _providerRepository.GetByIdAsync(invite.ProviderId);
        if (provider == null || emails == null || !emails.Any())
        {
            throw new NotFoundException();
        }

        var providerUsers = new List<ProviderUser>();
        foreach (var email in emails)
        {
            // Make sure user is not already invited
            var existingProviderUserCount =
                await _providerUserRepository.GetCountByProviderAsync(invite.ProviderId, email, false);
            if (existingProviderUserCount > 0)
            {
                continue;
            }

            var providerUser = new ProviderUser
            {
                ProviderId = invite.ProviderId,
                UserId = null,
                Email = email.ToLowerInvariant(),
                Key = null,
                Type = invite.Type,
                Status = ProviderUserStatusType.Invited,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow,
            };

            await _providerUserRepository.CreateAsync(providerUser);

            await SendInviteAsync(providerUser, provider);
            providerUsers.Add(providerUser);
        }

        await _eventService.LogProviderUsersEventAsync(providerUsers.Select(pu => (pu, EventType.ProviderUser_Invited, null as DateTime?)));

        return providerUsers;
    }

    public async Task<List<Tuple<ProviderUser, string>>> ResendInvitesAsync(ProviderUserInvite<Guid> invite)
    {
        if (!_currentContext.ProviderManageUsers(invite.ProviderId))
        {
            throw new BadRequestException("Invalid permissions.");
        }

        var providerUsers = await _providerUserRepository.GetManyAsync(invite.UserIdentifiers);
        var provider = await _providerRepository.GetByIdAsync(invite.ProviderId);

        var result = new List<Tuple<ProviderUser, string>>();
        foreach (var providerUser in providerUsers)
        {
            if (providerUser.Status != ProviderUserStatusType.Invited || providerUser.ProviderId != invite.ProviderId)
            {
                result.Add(Tuple.Create(providerUser, "User invalid."));
                continue;
            }

            await SendInviteAsync(providerUser, provider);
            result.Add(Tuple.Create(providerUser, ""));
        }

        return result;
    }

    public async Task<ProviderUser> AcceptUserAsync(Guid providerUserId, User user, string token)
    {
        var providerUser = await _providerUserRepository.GetByIdAsync(providerUserId);
        if (providerUser == null)
        {
            throw new BadRequestException("User invalid.");
        }

        if (providerUser.Status != ProviderUserStatusType.Invited)
        {
            throw new BadRequestException("Already accepted.");
        }

        if (!CoreHelpers.TokenIsValid("ProviderUserInvite", _dataProtector, token, user.Email, providerUser.Id,
            _globalSettings.OrganizationInviteExpirationHours))
        {
            throw new BadRequestException("Invalid token.");
        }

        if (string.IsNullOrWhiteSpace(providerUser.Email) ||
            !providerUser.Email.Equals(user.Email, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new BadRequestException("User email does not match invite.");
        }

        providerUser.Status = ProviderUserStatusType.Accepted;
        providerUser.UserId = user.Id;
        providerUser.Email = null;

        await _providerUserRepository.ReplaceAsync(providerUser);

        return providerUser;
    }

    public async Task<List<Tuple<ProviderUser, string>>> ConfirmUsersAsync(Guid providerId, Dictionary<Guid, string> keys,
        Guid confirmingUserId)
    {
        var providerUsers = await _providerUserRepository.GetManyAsync(keys.Keys);
        var validProviderUsers = providerUsers
            .Where(u => u.UserId != null)
            .ToList();

        if (!validProviderUsers.Any())
        {
            return new List<Tuple<ProviderUser, string>>();
        }

        var validOrganizationUserIds = validProviderUsers.Select(u => u.UserId.Value).ToList();

        var provider = await _providerRepository.GetByIdAsync(providerId);
        var users = await _userRepository.GetManyAsync(validOrganizationUserIds);

        var keyedFilteredUsers = validProviderUsers.ToDictionary(u => u.UserId.Value, u => u);

        var result = new List<Tuple<ProviderUser, string>>();
        var events = new List<(ProviderUser, EventType, DateTime?)>();

        foreach (var user in users)
        {
            if (!keyedFilteredUsers.ContainsKey(user.Id))
            {
                continue;
            }
            var providerUser = keyedFilteredUsers[user.Id];
            try
            {
                if (providerUser.Status != ProviderUserStatusType.Accepted || providerUser.ProviderId != providerId)
                {
                    throw new BadRequestException("Invalid user.");
                }

                providerUser.Status = ProviderUserStatusType.Confirmed;
                providerUser.Key = keys[providerUser.Id];
                providerUser.Email = null;

                await _providerUserRepository.ReplaceAsync(providerUser);
                events.Add((providerUser, EventType.ProviderUser_Confirmed, null));
                await _mailService.SendProviderConfirmedEmailAsync(provider.DisplayName(), user.Email);
                result.Add(Tuple.Create(providerUser, ""));
            }
            catch (BadRequestException e)
            {
                result.Add(Tuple.Create(providerUser, e.Message));
            }
        }

        await _eventService.LogProviderUsersEventAsync(events);

        return result;
    }

    public async Task SaveUserAsync(ProviderUser user, Guid savingUserId)
    {
        if (user.Id.Equals(default))
        {
            throw new BadRequestException("Invite the user first.");
        }

        if (user.Type != ProviderUserType.ProviderAdmin &&
            !await HasConfirmedProviderAdminExceptAsync(user.ProviderId, new[] { user.Id }))
        {
            throw new BadRequestException("Provider must have at least one confirmed ProviderAdmin.");
        }

        await _providerUserRepository.ReplaceAsync(user);
        await _eventService.LogProviderUserEventAsync(user, EventType.ProviderUser_Updated);
    }

    public async Task<List<Tuple<ProviderUser, string>>> DeleteUsersAsync(Guid providerId,
        IEnumerable<Guid> providerUserIds, Guid deletingUserId)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            throw new NotFoundException();
        }

        var providerUsers = await _providerUserRepository.GetManyAsync(providerUserIds);
        var users = await _userRepository.GetManyAsync(providerUsers.Where(pu => pu.UserId.HasValue)
            .Select(pu => pu.UserId.Value));
        var keyedUsers = users.ToDictionary(u => u.Id);

        if (!await HasConfirmedProviderAdminExceptAsync(providerId, providerUserIds))
        {
            throw new BadRequestException("Provider must have at least one confirmed ProviderAdmin.");
        }

        var result = new List<Tuple<ProviderUser, string>>();
        var deletedUserIds = new List<Guid>();
        var events = new List<(ProviderUser, EventType, DateTime?)>();

        foreach (var providerUser in providerUsers)
        {
            try
            {
                if (providerUser.ProviderId != providerId)
                {
                    throw new BadRequestException("Invalid user.");
                }
                if (providerUser.UserId == deletingUserId)
                {
                    throw new BadRequestException("You cannot remove yourself.");
                }

                events.Add((providerUser, EventType.ProviderUser_Removed, null));

                var user = keyedUsers.GetValueOrDefault(providerUser.UserId.GetValueOrDefault());
                var email = user == null ? providerUser.Email : user.Email;
                if (!string.IsNullOrWhiteSpace(email))
                {
                    await _mailService.SendProviderUserRemoved(provider.DisplayName(), email);
                }

                result.Add(Tuple.Create(providerUser, ""));
                deletedUserIds.Add(providerUser.Id);
            }
            catch (BadRequestException e)
            {
                result.Add(Tuple.Create(providerUser, e.Message));
            }

            await _providerUserRepository.DeleteManyAsync(deletedUserIds);
        }

        await _eventService.LogProviderUsersEventAsync(events);

        return result;
    }

    public async Task AddOrganization(Guid providerId, Guid organizationId, string key)
    {
        var po = await _providerOrganizationRepository.GetByOrganizationId(organizationId);
        if (po != null)
        {
            throw new BadRequestException("Organization already belongs to a provider.");
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId);

        var provider = await _providerRepository.GetByIdAsync(providerId);

        ThrowOnInvalidPlanType(provider.Type, organization.PlanType);

        if (organization.UseSecretsManager)
        {
            throw new BadRequestException(
                "The organization is subscribed to Secrets Manager. Please contact Customer Support to manage the subscription.");
        }

        var providerOrganization = new ProviderOrganization
        {
            ProviderId = providerId,
            OrganizationId = organizationId,
            Key = key,
        };

        await ApplyProviderPriceRateAsync(organization, provider);
        await _providerOrganizationRepository.CreateAsync(providerOrganization);

        organization.BillingEmail = provider.BillingEmail;
        await _organizationRepository.ReplaceAsync(organization);

        if (!string.IsNullOrEmpty(organization.GatewayCustomerId))
        {
            await _stripeAdapter.CustomerUpdateAsync(organization.GatewayCustomerId, new CustomerUpdateOptions
            {
                Email = provider.BillingEmail
            });
        }

        await _eventService.LogProviderOrganizationEventAsync(providerOrganization, EventType.ProviderOrganization_Added);
    }

    public async Task AddOrganizationsToReseller(Guid providerId, IEnumerable<Guid> organizationIds)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider.Type != ProviderType.Reseller)
        {
            throw new BadRequestException("Provider must be of type Reseller in order to assign Organizations to it.");
        }

        var orgIdsList = organizationIds.ToList();
        var existingProviderOrganizationsCount = await _providerOrganizationRepository.GetCountByOrganizationIdsAsync(orgIdsList);
        if (existingProviderOrganizationsCount > 0)
        {
            throw new BadRequestException("Organizations must not be assigned to any Provider.");
        }

        var providerOrganizationsToInsert = orgIdsList.Select(orgId => new ProviderOrganization { ProviderId = providerId, OrganizationId = orgId });
        var insertedProviderOrganizations = await _providerOrganizationRepository.CreateManyAsync(providerOrganizationsToInsert);

        await _eventService.LogProviderOrganizationEventsAsync(insertedProviderOrganizations.Select(ipo => (ipo, EventType.ProviderOrganization_Added, (DateTime?)null)));
    }

    private async Task ApplyProviderPriceRateAsync(Organization organization, Provider provider)
    {
        // if a provider was created before Nov 6, 2023.If true, the organization plan assigned to that provider is updated to a 2020 plan.
        if (provider.CreationDate >= Constants.ProviderCreatedPriorNov62023)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            var subscriptionItem = await GetSubscriptionItemAsync(organization.GatewaySubscriptionId,
                GetStripeSeatPlanId(organization.PlanType));
            var extractedPlanType = PlanTypeMappings(organization);
            if (subscriptionItem != null)
            {
                await UpdateSubscriptionAsync(subscriptionItem, GetStripeSeatPlanId(extractedPlanType), organization);
            }
        }

        await _organizationRepository.UpsertAsync(organization);
    }

    private async Task<Stripe.SubscriptionItem> GetSubscriptionItemAsync(string subscriptionId, string oldPlanId)
    {
        var subscriptionDetails = await _stripeAdapter.SubscriptionGetAsync(subscriptionId);
        return subscriptionDetails.Items.Data.FirstOrDefault(item => item.Price.Id == oldPlanId);
    }

    private static string GetStripeSeatPlanId(PlanType planType)
    {
        return StaticStore.GetPlan(planType).PasswordManager.StripeSeatPlanId;
    }

    private async Task UpdateSubscriptionAsync(Stripe.SubscriptionItem subscriptionItem, string extractedPlanType, Organization organization)
    {
        try
        {
            if (subscriptionItem.Price.Id != extractedPlanType)
            {
                await _stripeAdapter.SubscriptionUpdateAsync(subscriptionItem.Subscription,
                    new Stripe.SubscriptionUpdateOptions
                    {
                        Items = new List<Stripe.SubscriptionItemOptions>
                        {
                            new()
                            {
                                Id = subscriptionItem.Id,
                                Price = extractedPlanType,
                                Quantity = organization.Seats.Value,
                            },
                        }
                    });
            }
        }
        catch (Exception)
        {
            throw new Exception("Unable to update existing plan on stripe");
        }

    }

    private static PlanType PlanTypeMappings(Organization organization)
    {
        var planTypeMappings = new Dictionary<PlanType, string>
        {
            { PlanType.EnterpriseAnnually2020, GetEnumDisplayName(PlanType.EnterpriseAnnually2020) },
            { PlanType.EnterpriseMonthly2020, GetEnumDisplayName(PlanType.EnterpriseMonthly2020) },
            { PlanType.TeamsMonthly2020, GetEnumDisplayName(PlanType.TeamsMonthly2020) },
            { PlanType.TeamsAnnually2020, GetEnumDisplayName(PlanType.TeamsAnnually2020) }
        };

        foreach (var mapping in planTypeMappings)
        {
            if (mapping.Value.IndexOf(organization.Plan, StringComparison.Ordinal) != -1)
            {
                organization.PlanType = mapping.Key;
                organization.Plan = mapping.Value;
                return organization.PlanType;
            }
        }

        throw new ArgumentException("Invalid PlanType selected");
    }

    private static string GetEnumDisplayName(Enum value)
    {
        var fieldInfo = value.GetType().GetField(value.ToString());

        var displayAttribute = (DisplayAttribute)Attribute.GetCustomAttribute(fieldInfo!, typeof(DisplayAttribute));

        return displayAttribute?.Name ?? value.ToString();
    }

    public async Task<ProviderOrganization> CreateOrganizationAsync(Guid providerId,
        OrganizationSignup organizationSignup, string clientOwnerEmail, User user)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);

        ThrowOnInvalidPlanType(provider.Type, organizationSignup.Plan);

        var (organization, _, defaultCollection) = await _organizationService.SignupClientAsync(organizationSignup);

        var providerOrganization = new ProviderOrganization
        {
            ProviderId = providerId,
            OrganizationId = organization.Id,
            Key = organizationSignup.OwnerKey,
        };

        await _providerOrganizationRepository.CreateAsync(providerOrganization);
        await _eventService.LogProviderOrganizationEventAsync(providerOrganization, EventType.ProviderOrganization_Created);

        // Give the owner Can Manage access over the default collection
        // The orgUser is not available when the org is created so we have to do it here as part of the invite
        var defaultOwnerAccess = defaultCollection != null
            ?
            [
                new CollectionAccessSelection
                {
                    Id = defaultCollection.Id,
                    HidePasswords = false,
                    ReadOnly = false,
                    Manage = true
                }
            ]
            : Array.Empty<CollectionAccessSelection>();

        await _organizationService.InviteUsersAsync(organization.Id, user.Id, systemUser: null,
            new (OrganizationUserInvite, string)[]
            {
                (
                    new OrganizationUserInvite
                    {
                        Emails = new[] { clientOwnerEmail },
                        Type = OrganizationUserType.Owner,
                        Permissions = null,
                        Collections = defaultOwnerAccess,
                    },
                    null
                )
            });

        return providerOrganization;
    }

    public async Task ResendProviderSetupInviteEmailAsync(Guid providerId, Guid ownerId)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        var owner = await _userRepository.GetByIdAsync(ownerId);
        if (owner == null)
        {
            throw new BadRequestException("Invalid owner.");
        }
        await SendProviderSetupInviteEmailAsync(provider, owner.Email);
    }

    public async Task SendProviderSetupInviteEmailAsync(Provider provider, string ownerEmail)
    {
        var token = _dataProtector.Protect($"ProviderSetupInvite {provider.Id} {ownerEmail} {CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow)}");
        await _mailService.SendProviderSetupInviteEmailAsync(provider, token, ownerEmail);
    }

    public async Task LogProviderAccessToOrganizationAsync(Guid organizationId)
    {
        if (organizationId == default)
        {
            return;
        }

        var providerOrganization = await _providerOrganizationRepository.GetByOrganizationId(organizationId);
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (providerOrganization != null)
        {
            await _eventService.LogProviderOrganizationEventAsync(providerOrganization, EventType.ProviderOrganization_VaultAccessed);
        }
        if (organization != null)
        {
            await _eventService.LogOrganizationEventAsync(organization, EventType.Organization_VaultAccessed);
        }
    }

    public async Task InitiateDeleteAsync(Provider provider, string providerAdminEmail)
    {
        if (string.IsNullOrWhiteSpace(provider.Name))
        {
            throw new BadRequestException("Provider name not found.");
        }
        var providerAdmin = await _userRepository.GetByEmailAsync(providerAdminEmail);
        if (providerAdmin == null)
        {
            throw new BadRequestException("Provider admin not found.");
        }

        var providerAdminOrgUser = await _providerUserRepository.GetByProviderUserAsync(provider.Id, providerAdmin.Id);
        if (providerAdminOrgUser == null || providerAdminOrgUser.Status != ProviderUserStatusType.Confirmed ||
            providerAdminOrgUser.Type != ProviderUserType.ProviderAdmin)
        {
            throw new BadRequestException("Org admin not found.");
        }

        var token = _providerDeleteTokenDataFactory.Protect(new ProviderDeleteTokenable(provider, 1));
        await _mailService.SendInitiateDeletProviderEmailAsync(providerAdminEmail, provider, token);
    }

    public async Task DeleteAsync(Provider provider, string token)
    {
        if (!_providerDeleteTokenDataFactory.TryUnprotect(token, out var data) || !data.IsValid(provider))
        {
            throw new BadRequestException("Invalid token.");
        }
        await DeleteAsync(provider);
    }

    public async Task DeleteAsync(Provider provider)
    {
        await _providerRepository.DeleteAsync(provider);
        await _applicationCacheService.DeleteProviderAbilityAsync(provider.Id);
    }

    private async Task SendInviteAsync(ProviderUser providerUser, Provider provider)
    {
        var nowMillis = CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow);
        var token = _dataProtector.Protect(
            $"ProviderUserInvite {providerUser.Id} {providerUser.Email} {nowMillis}");
        await _mailService.SendProviderInviteEmailAsync(provider.DisplayName(), providerUser, token, providerUser.Email);
    }

    private async Task<bool> HasConfirmedProviderAdminExceptAsync(Guid providerId, IEnumerable<Guid> providerUserIds)
    {
        var providerAdmins = await _providerUserRepository.GetManyByProviderAsync(providerId,
            ProviderUserType.ProviderAdmin);
        var confirmedOwners = providerAdmins.Where(o => o.Status == ProviderUserStatusType.Confirmed);
        var confirmedOwnersIds = confirmedOwners.Select(u => u.Id);
        return confirmedOwnersIds.Except(providerUserIds).Any();
    }

    private void ThrowOnInvalidPlanType(ProviderType providerType, PlanType requestedType)
    {
        switch (providerType)
        {
            case ProviderType.Msp:
                if (requestedType is not (PlanType.TeamsMonthly or PlanType.EnterpriseMonthly))
                {
                    throw new BadRequestException($"Managed Service Providers cannot manage organizations with the plan type {requestedType}. Only Teams (Monthly) and Enterprise (Monthly) are allowed.");
                }
                break;
            case ProviderType.MultiOrganizationEnterprise:
                if (requestedType is not (PlanType.EnterpriseMonthly or PlanType.EnterpriseAnnually))
                {
                    throw new BadRequestException($"Multi-organization Enterprise Providers cannot manage organizations with the plan type {requestedType}. Only Enterprise (Monthly) and Enterprise (Annually) are allowed.");
                }
                break;
            default:
                throw new BadRequestException($"Unsupported provider type {providerType}.");
        }

        if (ProviderDisallowedOrganizationTypes.Contains(requestedType))
        {
            throw new BadRequestException($"Providers cannot manage organizations with the requested plan type ({requestedType}). Only Teams and Enterprise accounts are allowed.");
        }
    }
}
