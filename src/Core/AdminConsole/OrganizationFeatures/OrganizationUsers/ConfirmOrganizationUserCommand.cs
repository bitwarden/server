// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationConfirmation;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.Auth.UserFeatures.EmergencyAccess.Interfaces;
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
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IPushRegistrationService _pushRegistrationService;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IPolicyRequirementQuery _policyRequirementQuery;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IAutomaticUserConfirmationPolicyEnforcementValidator _automaticUserConfirmationPolicyEnforcementValidator;
    private readonly ISendOrganizationConfirmationCommand _sendOrganizationConfirmationCommand;
    private readonly IDeleteEmergencyAccessCommand _deleteEmergencyAccessCommand;

    public ConfirmOrganizationUserCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        IEventService eventService,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
        IPushNotificationService pushNotificationService,
        IPushRegistrationService pushRegistrationService,
        IDeviceRepository deviceRepository,
        IPolicyRequirementQuery policyRequirementQuery,
        ICollectionRepository collectionRepository,
        IAutomaticUserConfirmationPolicyEnforcementValidator automaticUserConfirmationPolicyEnforcementValidator,
        ISendOrganizationConfirmationCommand sendOrganizationConfirmationCommand,
        IDeleteEmergencyAccessCommand deleteEmergencyAccessCommand)
    {
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _userRepository = userRepository;
        _eventService = eventService;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
        _pushNotificationService = pushNotificationService;
        _pushRegistrationService = pushRegistrationService;
        _deviceRepository = deviceRepository;
        _policyRequirementQuery = policyRequirementQuery;
        _collectionRepository = collectionRepository;
        _automaticUserConfirmationPolicyEnforcementValidator = automaticUserConfirmationPolicyEnforcementValidator;
        _sendOrganizationConfirmationCommand = sendOrganizationConfirmationCommand;
        _deleteEmergencyAccessCommand = deleteEmergencyAccessCommand;
    }
    public async Task<OrganizationUser> ConfirmUserAsync(Guid organizationId, Guid organizationUserId, string key,
        Guid confirmingUserId, string defaultUserCollectionName = null)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);

        var result = await SaveChangesToDatabaseAsync(
            organizationId,
            new Dictionary<Guid, string>() { { organizationUserId, key } },
            confirmingUserId,
            organization);

        if (!result.Any())
        {
            throw new BadRequestException("User not valid.");
        }

        var (orgUser, error) = result[0];
        if (error != "")
        {
            throw new BadRequestException(error);
        }

        await CreateManyDefaultCollectionsAsync(organization, [orgUser], defaultUserCollectionName);

        return orgUser;
    }

    public async Task<List<Tuple<OrganizationUser, string>>> ConfirmUsersAsync(Guid organizationId, Dictionary<Guid, string> keys,
        Guid confirmingUserId, string defaultUserCollectionName = null)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);

        var result = await SaveChangesToDatabaseAsync(organizationId, keys, confirmingUserId, organization);

        var confirmedOrganizationUsers = result
            .Where(r => string.IsNullOrEmpty(r.Item2))
            .Select(r => r.Item1)
            .ToList();

        await CreateManyDefaultCollectionsAsync(organization, confirmedOrganizationUsers, defaultUserCollectionName);

        return result;
    }

    private async Task<List<Tuple<OrganizationUser, string>>> SaveChangesToDatabaseAsync(Guid organizationId, Dictionary<Guid, string> keys,
        Guid confirmingUserId, Organization organization)
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
                await SendOrganizationConfirmedEmailAsync(organization, user.Email, orgUser.AccessSecretsManager);
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
        ICollection<OrganizationUser> orgUsers, bool userTwoFactorEnabled)
    {
        // Enforce Two Factor Authentication Policy for this organization
        await ValidateTwoFactorAuthenticationPolicyAsync(user, organizationId, userTwoFactorEnabled);

        var policyRequirement = await _policyRequirementQuery.GetAsync<AutomaticUserConfirmationPolicyRequirement>(
            user.Id);

        var error = (await _automaticUserConfirmationPolicyEnforcementValidator.IsCompliantAsync(
                new AutomaticUserConfirmationPolicyEnforcementRequest(
                    organizationId,
                    orgUsers,
                    user),
                policyRequirement))
            .Match(
                error => new BadRequestException(error.Message),
                _ => null
            );

        if (error is not null)
        {
            throw error;
        }

        if (policyRequirement.IsEnabled(organizationId))
        {
            await _deleteEmergencyAccessCommand.DeleteAllByUserIdAsync(user.Id);
        }

        var singleOrgRequirement = await _policyRequirementQuery.GetAsync<SingleOrganizationPolicyRequirement>(user.Id);
        var singleOrgError = singleOrgRequirement.CanJoinOrganization(organizationId, orgUsers);
        if (singleOrgError is not null)
        {
            var singleOrgErrorMessage = singleOrgError switch
            {
                UserIsAMemberOfAnotherOrganization => $"{user.Email} cannot be confirmed until they leave or remove all other organizations.",
                UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy => $"{user.Email} cannot be confirmed because they are in another organization which forbids it.",
                _ => singleOrgError.Message
            };

            throw new BadRequestException(singleOrgErrorMessage);
        }
    }

    private async Task ValidateTwoFactorAuthenticationPolicyAsync(User user, Guid organizationId, bool userTwoFactorEnabled)
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
    /// Creates default collections for multiple users if required by the Organization Data Ownership policy.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <param name="confirmedOrganizationUsers">The confirmed organization users.</param>
    /// <param name="defaultUserCollectionName">The encrypted default user collection name.</param>
    private async Task CreateManyDefaultCollectionsAsync(Organization organization,
        IEnumerable<OrganizationUser> confirmedOrganizationUsers, string defaultUserCollectionName)
    {
        // Skip if no collection name provided (backwards compatibility)
        if (string.IsNullOrWhiteSpace(defaultUserCollectionName))
        {
            return;
        }

        // Skip if organization has disabled My Items
        if (!organization.UseMyItems)
        {
            return;
        }

        var confirmedUserIds = confirmedOrganizationUsers
            .Select(s => s.UserId!.Value)
            .ToList();

        var policiesForUsers = await _policyRequirementQuery
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(confirmedUserIds);

        var eligibleOrganizationUserIds = policiesForUsers
            .Select(x => x.Requirement.GetDefaultCollectionRequestOnConfirm(organization.Id))
            .Where(w => w.ShouldCreateDefaultCollection)
            .Select(s => s.OrganizationUserId)
            .ToList();

        if (eligibleOrganizationUserIds.Count == 0)
        {
            return;
        }

        await _collectionRepository.CreateDefaultCollectionsAsync(organization.Id, eligibleOrganizationUserIds, defaultUserCollectionName);
    }

    /// <summary>
    /// Sends the organization confirmed email using the new mailer pattern.
    /// </summary>
    /// <param name="organization">The organization the user was confirmed to.</param>
    /// <param name="userEmail">The email address of the confirmed user.</param>
    /// <param name="accessSecretsManager">Whether the user has access to Secrets Manager.</param>
    internal async Task SendOrganizationConfirmedEmailAsync(Organization organization, string userEmail, bool accessSecretsManager)
    {
        await _sendOrganizationConfirmationCommand.SendConfirmationAsync(organization, userEmail, accessSecretsManager);
    }
}
