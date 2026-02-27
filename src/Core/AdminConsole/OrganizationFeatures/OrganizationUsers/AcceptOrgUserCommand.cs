// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers;

public class AcceptOrgUserCommand : IAcceptOrgUserCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IPolicyService _policyService;
    private readonly IMailService _mailService;
    private readonly IUserRepository _userRepository;
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;
    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory;
    private readonly IFeatureService _featureService;
    private readonly IPolicyRequirementQuery _policyRequirementQuery;
    private readonly IAutomaticUserConfirmationPolicyEnforcementValidator _automaticUserConfirmationPolicyEnforcementValidator;
    private readonly IPushAutoConfirmNotificationCommand _pushAutoConfirmNotificationCommand;

    public AcceptOrgUserCommand(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyService policyService,
        IMailService mailService,
        IUserRepository userRepository,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
        IDataProtectorTokenFactory<OrgUserInviteTokenable> orgUserInviteTokenDataFactory,
        IFeatureService featureService,
        IPolicyRequirementQuery policyRequirementQuery,
        IAutomaticUserConfirmationPolicyEnforcementValidator automaticUserConfirmationPolicyEnforcementValidator,
        IPushAutoConfirmNotificationCommand pushAutoConfirmNotificationCommand)
    {
        _organizationUserRepository = organizationUserRepository;
        _organizationRepository = organizationRepository;
        _policyService = policyService;
        _mailService = mailService;
        _userRepository = userRepository;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
        _orgUserInviteTokenDataFactory = orgUserInviteTokenDataFactory;
        _featureService = featureService;
        _policyRequirementQuery = policyRequirementQuery;
        _automaticUserConfirmationPolicyEnforcementValidator = automaticUserConfirmationPolicyEnforcementValidator;
        _pushAutoConfirmNotificationCommand = pushAutoConfirmNotificationCommand;
    }

    public async Task<OrganizationUser> AcceptOrgUserByEmailTokenAsync(Guid organizationUserId, User user, string emailToken,
        IUserService userService)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (orgUser == null)
        {
            throw new BadRequestException("User invalid.");
        }

        var tokenValid = OrgUserInviteTokenable.ValidateOrgUserInviteStringToken(
            _orgUserInviteTokenDataFactory, emailToken, orgUser);

        if (!tokenValid)
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

        var organizationUser = await AcceptOrgUserAsync(orgUser, user, userService);

        // Verify user email if they accept org invite via email link
        if (user.EmailVerified == false)
        {
            user.EmailVerified = true;
            await _userRepository.ReplaceAsync(user);
        }

        return organizationUser;
    }

    public async Task<OrganizationUser> AcceptOrgUserByOrgSsoIdAsync(string orgSsoIdentifier, User user, IUserService userService)
    {
        var org = await _organizationRepository.GetByIdentifierAsync(orgSsoIdentifier);
        if (org == null)
        {
            throw new BadRequestException("Organization invalid.");
        }

        var orgUser = await _organizationUserRepository.GetByOrganizationAsync(org.Id, user.Id);
        if (orgUser == null)
        {
            throw new BadRequestException("User not found within organization.");
        }

        return await AcceptOrgUserAsync(orgUser, user, userService);
    }

    public async Task<OrganizationUser> AcceptOrgUserByOrgIdAsync(Guid organizationId, User user, IUserService userService)
    {
        var org = await _organizationRepository.GetByIdAsync(organizationId);
        if (org == null)
        {
            throw new BadRequestException("Organization invalid.");
        }

        var orgUser = await _organizationUserRepository.GetByOrganizationAsync(org.Id, user.Id);
        if (orgUser == null)
        {
            throw new BadRequestException("User not found within organization.");
        }

        return await AcceptOrgUserAsync(orgUser, user, userService);
    }

    public async Task<OrganizationUser> AcceptOrgUserAsync(OrganizationUser orgUser, User user,
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
            var org = await _organizationRepository.GetByIdAsync(orgUser.OrganizationId);
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

        var allOrgUsers = await _organizationUserRepository.GetManyByUserAsync(user.Id);

        if (_featureService.IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers))
        {
            await ValidateAutomaticUserConfirmationPolicyAsync(orgUser, allOrgUsers, user);
        }

        // Enforce Single Organization Policy of organization user is trying to join
        var invitedSingleOrgPolicies = await _policyService.GetPoliciesApplicableToUserAsync(user.Id,
            PolicyType.SingleOrg, OrganizationUserStatusType.Invited);

        if (allOrgUsers.Any(ou => ou.OrganizationId != orgUser.OrganizationId)
            && invitedSingleOrgPolicies.Any(p => p.OrganizationId == orgUser.OrganizationId))
        {
            throw new BadRequestException("You may not join this organization until you leave or remove all other organizations.");
        }

        // Enforce Single Organization Policy of other organizations user is a member of
        var anySingleOrgPolicies = await _policyService.AnyPoliciesApplicableToUserAsync(user.Id,
            PolicyType.SingleOrg);
        if (anySingleOrgPolicies)
        {
            throw new BadRequestException("You cannot join this organization because you are a member of another organization which forbids it");
        }

        // Enforce Two Factor Authentication Policy of organization user is trying to join
        await ValidateTwoFactorAuthenticationPolicyAsync(user, orgUser.OrganizationId);

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

        if (_featureService.IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers))
        {
            await _pushAutoConfirmNotificationCommand.PushAsync(user.Id, orgUser.OrganizationId);
        }

        return orgUser;
    }

    private async Task ValidateTwoFactorAuthenticationPolicyAsync(User user, Guid organizationId)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements))
        {
            if (await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(user))
            {
                // If the user has two-step login enabled, we skip checking the 2FA policy
                return;
            }

            var twoFactorPolicyRequirement = await _policyRequirementQuery.GetAsync<RequireTwoFactorPolicyRequirement>(user.Id);
            if (twoFactorPolicyRequirement.IsTwoFactorRequiredForOrganization(organizationId))
            {
                throw new BadRequestException("You cannot join this organization until you enable two-step login on your user account.");
            }

            return;
        }

        if (!await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(user))
        {
            var invitedTwoFactorPolicies = await _policyService.GetPoliciesApplicableToUserAsync(user.Id,
                PolicyType.TwoFactorAuthentication, OrganizationUserStatusType.Invited);
            if (invitedTwoFactorPolicies.Any(p => p.OrganizationId == organizationId))
            {
                throw new BadRequestException("You cannot join this organization until you enable two-step login on your user account.");
            }
        }
    }

    private async Task ValidateAutomaticUserConfirmationPolicyAsync(OrganizationUser orgUser,
        ICollection<OrganizationUser> allOrgUsers, User user)
    {
        var error = (await _automaticUserConfirmationPolicyEnforcementValidator.IsCompliantAsync(
                new AutomaticUserConfirmationPolicyEnforcementRequest(orgUser.OrganizationId,
                    allOrgUsers.Append(orgUser),
                    user)))
            .Match(
                error => error.Message,
                _ => string.Empty
            );

        if (!string.IsNullOrEmpty(error))
        {
            throw new BadRequestException(error);
        }
    }
}
