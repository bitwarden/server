using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
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
        IFeatureService featureService)
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
    }

    public async Task<OrganizationUser> ConfirmUserAsync(Guid organizationId, Guid organizationUserId, string key,
        Guid confirmingUserId)
    {
        var result = await ConfirmUsersAsync(
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
        return orgUser;
    }

    public async Task<List<Tuple<OrganizationUser, string>>> ConfirmUsersAsync(Guid organizationId, Dictionary<Guid, string> keys,
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

    private async Task CheckPoliciesAsync(Guid organizationId, User user,
        ICollection<OrganizationUser> userOrgs, bool userTwoFactorEnabled)
    {
        // Enforce Two Factor Authentication Policy for this organization
        if (_featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements))
        {
            await ValidateTwoFactorAuthenticationPolicyAsync(user, organizationId, userTwoFactorEnabled);
        }
        else
        {
            var orgRequiresTwoFactor = (await _policyService.GetPoliciesApplicableToUserAsync(user.Id, PolicyType.TwoFactorAuthentication))
                .Any(p => p.OrganizationId == organizationId);
            if (orgRequiresTwoFactor && !userTwoFactorEnabled)
            {
                throw new BadRequestException("User does not have two-step login enabled.");
            }
        }

        var hasOtherOrgs = userOrgs.Any(ou => ou.OrganizationId != organizationId);
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

    /// <summary>
    /// Validates the two-factor authentication policy for the organization user.
    /// If the policy applies to the organization, the user must have two-step login enabled.
    /// </summary>
    /// <param name="user">The user to validate the policy for.</param>
    /// <param name="organizationId">The ID of the organization to validate the policy for.</param>
    /// <param name="userTwoFactorEnabled">Whether the user has two-step login enabled.</param>
    /// <exception cref="BadRequestException">Thrown if the policy applies to the organization and 
    /// the user does not have two-step login enabled.</exception>
    private async Task ValidateTwoFactorAuthenticationPolicyAsync(User user, Guid organizationId, bool userTwoFactorEnabled)
    {
        if (userTwoFactorEnabled)
        {
            // If the user has two-step login enabled, we skip checking the 2FA policy
            return;
        }

        var twoFactorPolicyRequirement = await _policyRequirementQuery.GetAsync<RequireTwoFactorPolicyRequirement>(user.Id);
        var twoFactorRequired = twoFactorPolicyRequirement.IsTwoFactorRequiredForOrganization(organizationId);

        if (twoFactorRequired)
        {
            throw new BadRequestException("User does not have two-step login enabled.");
        }
    }

    private async Task DeleteAndPushUserRegistrationAsync(Guid organizationId, Guid userId)
    {
        var devices = await GetUserDeviceIdsAsync(userId);
        await _pushRegistrationService.DeleteUserRegistrationOrganizationAsync(devices,
            organizationId.ToString());
        await _pushNotificationService.PushSyncOrgKeysAsync(userId);
    }

    private async Task<IEnumerable<string>> GetUserDeviceIdsAsync(Guid userId)
    {
        var devices = await _deviceRepository.GetManyByUserIdAsync(userId);
        return devices
            .Where(d => !string.IsNullOrWhiteSpace(d.PushToken))
            .Select(d => d.Id.ToString());
    }
}
