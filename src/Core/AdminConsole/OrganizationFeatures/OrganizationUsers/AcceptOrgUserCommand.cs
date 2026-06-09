// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.UserFeatures.EmergencyAccess.Interfaces;
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
    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory;
    private readonly IAcceptOrganizationMembershipValidator _acceptOrganizationMembershipValidator;
    private readonly IPushAutoConfirmNotificationCommand _pushAutoConfirmNotificationCommand;
    private readonly IDeleteEmergencyAccessCommand _deleteEmergencyAccessCommand;

    public AcceptOrgUserCommand(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IMailService mailService,
        IUserRepository userRepository,
        IDataProtectorTokenFactory<OrgUserInviteTokenable> orgUserInviteTokenDataFactory,
        IAcceptOrganizationMembershipValidator acceptOrganizationMembershipValidator,
        IPushAutoConfirmNotificationCommand pushAutoConfirmNotificationCommand,
        IDeleteEmergencyAccessCommand deleteEmergencyAccessCommand)
    {
        _organizationUserRepository = organizationUserRepository;
        _organizationRepository = organizationRepository;
        _mailService = mailService;
        _userRepository = userRepository;
        _orgUserInviteTokenDataFactory = orgUserInviteTokenDataFactory;
        _acceptOrganizationMembershipValidator = acceptOrganizationMembershipValidator;
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

        var membershipValidationResult = await ValidateMembershipAsync(orgUser, user, allOrgUsers);
        if (membershipValidationResult.RequiresEmergencyAccessDeletion)
        {
            await _deleteEmergencyAccessCommand.DeleteAllByUserIdAsync(user.Id);
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

        await _pushAutoConfirmNotificationCommand.PushAsync(user.Id, orgUser.OrganizationId);

        return orgUser;
    }

    private async Task<AcceptOrganizationMembershipValidationResult> ValidateMembershipAsync(
        OrganizationUser orgUser, User user, ICollection<OrganizationUser> allOrgUsers)
    {
        var request = new AcceptOrganizationMembershipValidationRequest
        {
            OrganizationId = orgUser.OrganizationId,
            User = user,
            AllOrganizationMemberships = allOrgUsers,
            ExistingMembership = orgUser,
        };

        var result = await _acceptOrganizationMembershipValidator.ValidateAsync(request);
        if (result.IsError)
        {
            var message = result.AsError switch
            {
                UserIsAMemberOfAnotherOrganization => "You cannot accept this invite until you leave or remove all other organizations.",
                UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy => "You cannot accept this invite because you are in another organization which forbids it.",
                _ => result.AsError.Message
            };
            throw new BadRequestException(message);
        }

        return result.Request;
    }
}

