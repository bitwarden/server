// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.UserFeatures.EmergencyAccess.Interfaces;
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
    private readonly IMailService _mailService;
    private readonly IUserRepository _userRepository;
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;
    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory;
    private readonly IFeatureService _featureService;
    private readonly IPolicyRequirementQuery _policyRequirementQuery;
    private readonly IAutomaticUserConfirmationPolicyEnforcementValidator _automaticUserConfirmationPolicyEnforcementValidator;
    private readonly IPushAutoConfirmNotificationCommand _pushAutoConfirmNotificationCommand;
    private readonly IDeleteEmergencyAccessCommand _deleteEmergencyAccessCommand;

    public AcceptOrgUserCommand(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IMailService mailService,
        IUserRepository userRepository,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
        IDataProtectorTokenFactory<OrgUserInviteTokenable> orgUserInviteTokenDataFactory,
        IFeatureService featureService,
        IPolicyRequirementQuery policyRequirementQuery,
        IAutomaticUserConfirmationPolicyEnforcementValidator automaticUserConfirmationPolicyEnforcementValidator,
        IPushAutoConfirmNotificationCommand pushAutoConfirmNotificationCommand,
        IDeleteEmergencyAccessCommand deleteEmergencyAccessCommand)
    {
        _organizationUserRepository = organizationUserRepository;
        _organizationRepository = organizationRepository;
        _mailService = mailService;
        _userRepository = userRepository;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
        _orgUserInviteTokenDataFactory = orgUserInviteTokenDataFactory;
        _featureService = featureService;
        _policyRequirementQuery = policyRequirementQuery;
        _automaticUserConfirmationPolicyEnforcementValidator = automaticUserConfirmationPolicyEnforcementValidator;
        _pushAutoConfirmNotificationCommand = pushAutoConfirmNotificationCommand;
        _deleteEmergencyAccessCommand = deleteEmergencyAccessCommand;
    }

    public async Task<OrganizationUser> AcceptOrgUserByEmailTokenAsync(Guid organizationUserId, User user, string emailToken,
        IUserService userService)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (orgUser == null)
        {
            throw new BadRequestException("User invalid.");
        }

        var tokenValidationError = OrgUserInviteTokenable.ValidateOrgUserInvite(
            _orgUserInviteTokenDataFactory, emailToken, orgUser.Id, orgUser.Email);

        if (tokenValidationError != null)
        {
            throw new BadRequestException(tokenValidationError.ErrorMessage);
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
            await HandleAutomaticUserConfirmationPolicyAsync(orgUser, allOrgUsers, user);
        }

        await ValidateSingleOrganizationPolicyAsync(orgUser, allOrgUsers, user);

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

    private async Task ValidateSingleOrganizationPolicyAsync(OrganizationUser orgUser, ICollection<OrganizationUser> allOrgUsers, User user)
    {
        var singleOrgRequirement = await _policyRequirementQuery.GetAsync<SingleOrganizationPolicyRequirement>(user.Id);
        var error = singleOrgRequirement.CanJoinOrganization(orgUser.OrganizationId, allOrgUsers);
        if (error is not null)
        {
            var singleOrgErrorMessage = error switch
            {
                UserIsAMemberOfAnotherOrganization => "You cannot accept this invite until you leave or remove all other organizations.",
                UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy => "You cannot accept this invite because you are in another organization which forbids it.",
                _ => error.Message
            };

            throw new BadRequestException(singleOrgErrorMessage);
        }
    }

    private async Task ValidateTwoFactorAuthenticationPolicyAsync(User user, Guid organizationId)
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
    }

    private async Task HandleAutomaticUserConfirmationPolicyAsync(OrganizationUser orgUser,
        ICollection<OrganizationUser> allOrgUsers,
        User user)
    {
        var policyRequirement = await _policyRequirementQuery.GetAsync<AutomaticUserConfirmationPolicyRequirement>(
            user.Id);

        var error = (await _automaticUserConfirmationPolicyEnforcementValidator.IsCompliantAsync(
                new AutomaticUserConfirmationPolicyEnforcementRequest(orgUser.OrganizationId,
                    allOrgUsers.Append(orgUser),
                    user),
                policyRequirement))
            .Match(
                error => error.Message,
                _ => string.Empty
            );

        if (!string.IsNullOrEmpty(error))
        {
            throw new BadRequestException(error);
        }

        if (policyRequirement.IsEnabled(orgUser.OrganizationId))
        {
            await _deleteEmergencyAccessCommand.DeleteAllByUserIdAsync(user.Id);
        }
    }
}
