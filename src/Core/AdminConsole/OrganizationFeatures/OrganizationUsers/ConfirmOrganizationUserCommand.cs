// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class ConfirmOrganizationUserCommand : IConfirmOrganizationUserCommand
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEventService _eventService;
    private readonly IMailService _mailService;
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IPushRegistrationService _pushRegistrationService;
    private readonly IPolicyService _policyService;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IPolicyRequirementQuery _policyRequirementQuery;
    private readonly IFeatureService _featureService;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IAutomaticUserConfirmationPolicyEnforcementValidator _automaticUserConfirmationPolicyEnforcementValidator;

    public ConfirmOrganizationUserCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        IEventService eventService,
        IMailService mailService,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
        IPushNotificationService pushNotificationService,
        IPushRegistrationService pushRegistrationService,
        IPolicyService policyService,
        IDeviceRepository deviceRepository,
        IPolicyRequirementQuery policyRequirementQuery,
        IFeatureService featureService,
        ICollectionRepository collectionRepository,
        IAutomaticUserConfirmationPolicyEnforcementValidator automaticUserConfirmationPolicyEnforcementValidator)
    {
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _userRepository = userRepository;
        _eventService = eventService;
        _mailService = mailService;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
        _pushNotificationService = pushNotificationService;
        _pushRegistrationService = pushRegistrationService;
        _policyService = policyService;
        _deviceRepository = deviceRepository;
        _policyRequirementQuery = policyRequirementQuery;
        _featureService = featureService;
        _collectionRepository = collectionRepository;
        _automaticUserConfirmationPolicyEnforcementValidator = automaticUserConfirmationPolicyEnforcementValidator;
    }

    public async Task<OrganizationUser> ConfirmUserAsync(Guid organizationId, Guid organizationUserId, string key,
        Guid confirmingUserId, string defaultUserCollectionName = null)
    {
        var result = await SaveChangesToDatabaseAsync(
            organizationId,
            new Dictionary<Guid, string>() { { organizationUserId, key } },
            confirmingUserId);

        if (!result.Any())
        {
            throw new BadRequestException("User not valid.");
        }

        var (orgUser, error) = result[0];
        if (error != "")
        {
            throw new BadRequestException(error);
        }

        await CreateDefaultCollectionAsync(orgUser, defaultUserCollectionName);

        return orgUser;
    }

    public async Task<List<Tuple<OrganizationUser, string>>> ConfirmUsersAsync(Guid organizationId, Dictionary<Guid, string> keys,
        Guid confirmingUserId, string defaultUserCollectionName = null)
    {
        var result = await SaveChangesToDatabaseAsync(organizationId, keys, confirmingUserId);

        var confirmedOrganizationUsers = result
            .Where(r => string.IsNullOrEmpty(r.Item2))
            .Select(r => r.Item1)
            .ToList();

        if (confirmedOrganizationUsers.Count == 1)
        {
            await CreateDefaultCollectionAsync(confirmedOrganizationUsers.Single(), defaultUserCollectionName);
        }
        else if (confirmedOrganizationUsers.Count > 1)
        {
            await CreateManyDefaultCollectionsAsync(organizationId, confirmedOrganizationUsers, defaultUserCollectionName);
        }

        return result;
    }

    private async Task<List<Tuple<OrganizationUser, string>>> SaveChangesToDatabaseAsync(Guid organizationId, Dictionary<Guid, string> keys,
        Guid confirmingUserId)
    {
        var selectedOrganizationUsers = await _organizationUserRepository.GetManyAsync(keys.Keys);
        var validSelectedOrganizationUsers = selectedOrganizationUsers
            .Where(u => u.Status == OrganizationUserStatusType.Accepted && u.OrganizationId == organizationId && u.UserId != null)
            .ToList();

        if (!validSelectedOrganizationUsers.Any())
        {
            return new List<Tuple<OrganizationUser, string>>();
        }

        var validSelectedUserIds = validSelectedOrganizationUsers.Select(u => u.UserId.Value).ToList();

        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        var allUsersOrgs = await _organizationUserRepository.GetManyByManyUsersAsync(validSelectedUserIds);

        var users = await _userRepository.GetManyAsync(validSelectedUserIds);
        var usersTwoFactorEnabled = await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(validSelectedUserIds);

        var keyedFilteredUsers = validSelectedOrganizationUsers.ToDictionary(u => u.UserId.Value, u => u);
        var keyedOrganizationUsers = allUsersOrgs.GroupBy(u => u.UserId.Value)
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

                var userTwoFactorEnabled = usersTwoFactorEnabled.FirstOrDefault(tuple => tuple.userId == user.Id).twoFactorIsEnabled;
                await CheckPoliciesAsync(organizationId, user, orgUsers, userTwoFactorEnabled);
                orgUser.Status = OrganizationUserStatusType.Confirmed;
                orgUser.Key = keys[orgUser.Id];
                orgUser.Email = null;

                await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
                await _mailService.SendOrganizationConfirmedEmailAsync(organization.DisplayName(), user.Email, orgUser.AccessSecretsManager);
                succeededUsers.Add(orgUser);
                result.Add(Tuple.Create(orgUser, ""));
            }
            catch (BadRequestException e)
            {
                result.Add(Tuple.Create(orgUser, e.Message));
            }
        }

        await _organizationUserRepository.ReplaceManyAsync(succeededUsers);
        await DeleteAndPushUserRegistrationAsync(organizationId, succeededUsers.Select(u => u.UserId!.Value));

        return result;
    }

    private async Task CheckPoliciesAsync(Guid organizationId, User user,
        ICollection<OrganizationUser> userOrgs, bool userTwoFactorEnabled)
    {
        // Enforce Two Factor Authentication Policy for this organization
        await ValidateTwoFactorAuthenticationPolicyAsync(user, organizationId, userTwoFactorEnabled);

        var hasOtherOrgs = userOrgs.Any(ou => ou.OrganizationId != organizationId);

        if (_featureService.IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers))
        {
            var error = (await _automaticUserConfirmationPolicyEnforcementValidator.IsCompliantAsync(
                    new AutomaticUserConfirmationPolicyEnforcementRequest(
                        organizationId,
                        userOrgs,
                        user)))
                .Match(
                    error => new BadRequestException(error.Message),
                    _ => null
                );

            if (error is not null)
            {
                throw error;
            }
        }

        var singleOrgPolicies = await _policyService.GetPoliciesApplicableToUserAsync(user.Id, PolicyType.SingleOrg);
        var otherSingleOrgPolicies =
            singleOrgPolicies.Where(p => p.OrganizationId != organizationId);
        // Enforce Single Organization Policy for this organization
        if (hasOtherOrgs && singleOrgPolicies.Any(p => p.OrganizationId == organizationId))
        {
            throw new BadRequestException("Cannot confirm this member to the organization until they leave or remove all other organizations.");
        }
        // Enforce Single Organization Policy of other organizations user is a member of
        if (otherSingleOrgPolicies.Any())
        {
            throw new BadRequestException("Cannot confirm this member to the organization because they are in another organization which forbids it.");
        }
    }

    private async Task ValidateTwoFactorAuthenticationPolicyAsync(User user, Guid organizationId, bool userTwoFactorEnabled)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements))
        {
            if (userTwoFactorEnabled)
            {
                // If the user has two-step login enabled, we skip checking the 2FA policy
                return;
            }

            var twoFactorPolicyRequirement = await _policyRequirementQuery.GetAsync<RequireTwoFactorPolicyRequirement>(user.Id);
            if (twoFactorPolicyRequirement.IsTwoFactorRequiredForOrganization(organizationId))
            {
                throw new BadRequestException("User does not have two-step login enabled.");
            }

            return;
        }

        var orgRequiresTwoFactor = (await _policyService.GetPoliciesApplicableToUserAsync(user.Id, PolicyType.TwoFactorAuthentication))
            .Any(p => p.OrganizationId == organizationId);
        if (orgRequiresTwoFactor && !userTwoFactorEnabled)
        {
            throw new BadRequestException("User does not have two-step login enabled.");
        }
    }

    private async Task DeleteAndPushUserRegistrationAsync(Guid organizationId, IEnumerable<Guid> userIds)
    {
        foreach (var userId in userIds)
        {
            var devices = await GetUserDeviceIdsAsync(userId);
            await _pushRegistrationService.DeleteUserRegistrationOrganizationAsync(devices,
                organizationId.ToString());
            await _pushNotificationService.PushSyncOrgKeysAsync(userId);
        }
    }

    private async Task<IEnumerable<string>> GetUserDeviceIdsAsync(Guid userId)
    {
        var devices = await _deviceRepository.GetManyByUserIdAsync(userId);
        return devices
            .Where(d => !string.IsNullOrWhiteSpace(d.PushToken))
            .Select(d => d.Id.ToString());
    }

    /// <summary>
    /// Creates a default collection for a single user if required by the Organization Data Ownership policy.
    /// </summary>
    /// <param name="organizationUser">The organization user who has just been confirmed.</param>
    /// <param name="defaultUserCollectionName">The encrypted default user collection name.</param>
    private async Task CreateDefaultCollectionAsync(OrganizationUser organizationUser, string defaultUserCollectionName)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.CreateDefaultLocation))
        {
            return;
        }

        // Skip if no collection name provided (backwards compatibility)
        if (string.IsNullOrWhiteSpace(defaultUserCollectionName))
        {
            return;
        }

        var organizationDataOwnershipPolicy = await _policyRequirementQuery.GetAsync<OrganizationDataOwnershipPolicyRequirement>(organizationUser.UserId!.Value);
        if (!organizationDataOwnershipPolicy.RequiresDefaultCollectionOnConfirm(organizationUser.OrganizationId))
        {
            return;
        }

        await _collectionRepository.UpsertDefaultCollectionAsync(
            organizationUser.OrganizationId,
            organizationUser.Id,
            defaultUserCollectionName);
    }

    /// <summary>
    /// Creates default collections for multiple users if required by the Organization Data Ownership policy.
    /// </summary>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="confirmedOrganizationUsers">The confirmed organization users.</param>
    /// <param name="defaultUserCollectionName">The encrypted default user collection name.</param>
    private async Task CreateManyDefaultCollectionsAsync(Guid organizationId,
        IEnumerable<OrganizationUser> confirmedOrganizationUsers, string defaultUserCollectionName)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.CreateDefaultLocation))
        {
            return;
        }

        // Skip if no collection name provided (backwards compatibility)
        if (string.IsNullOrWhiteSpace(defaultUserCollectionName))
        {
            return;
        }

        var policyEligibleOrganizationUserIds = await _policyRequirementQuery
            .GetManyByOrganizationIdAsync<OrganizationDataOwnershipPolicyRequirement>(organizationId);

        var eligibleOrganizationUserIds = confirmedOrganizationUsers
            .Where(ou => policyEligibleOrganizationUserIds.Contains(ou.Id))
            .Select(ou => ou.Id)
            .ToList();

        if (eligibleOrganizationUserIds.Count == 0)
        {
            return;
        }

        await _collectionRepository.UpsertDefaultCollectionsAsync(organizationId, eligibleOrganizationUserIds, defaultUserCollectionName);
    }
}
