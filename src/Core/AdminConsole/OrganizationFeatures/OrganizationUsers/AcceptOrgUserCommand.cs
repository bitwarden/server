// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Models.Business.Tokenables;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
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
    private readonly IUserRepository _userRepository;
    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory;
    private readonly IAcceptOrganizationMembershipValidator _acceptOrganizationMembershipValidator;
    private readonly INotifyAdminsOfAcceptedUserCommand _notifyAdminsOfAcceptedUserCommand;
    private readonly IDeleteEmergencyAccessCommand _deleteEmergencyAccessCommand;

    public AcceptOrgUserCommand(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IDataProtectorTokenFactory<OrgUserInviteTokenable> orgUserInviteTokenDataFactory,
        IAcceptOrganizationMembershipValidator acceptOrganizationMembershipValidator,
        INotifyAdminsOfAcceptedUserCommand notifyAdminsOfAcceptedUserCommand,
        IDeleteEmergencyAccessCommand deleteEmergencyAccessCommand)
    {
        _organizationUserRepository = organizationUserRepository;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _orgUserInviteTokenDataFactory = orgUserInviteTokenDataFactory;
        _acceptOrganizationMembershipValidator = acceptOrganizationMembershipValidator;
        _notifyAdminsOfAcceptedUserCommand = notifyAdminsOfAcceptedUserCommand;
        _deleteEmergencyAccessCommand = deleteEmergencyAccessCommand;
    }

    public async Task<OrganizationUser> AcceptOrgUserByEmailTokenAsync(Guid organizationUserId, User user, string emailToken,
        IUserService userService)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (orgUser == null)
        {
            throw new BadRequestException(new OrganizationUserNotFoundError().Message);
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
                throw new BadRequestException(new InvitationAlreadyAcceptedError().Message);
            }
            throw new BadRequestException(new AlreadyPartOfOrganizationError().Message);
        }

        if (string.IsNullOrWhiteSpace(orgUser.Email) ||
            !orgUser.Email.Equals(user.Email, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new BadRequestException(new EmailMismatchError().Message);
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
            throw new BadRequestException(new OrganizationNotFoundError().Message);
        }

        var orgUser = await _organizationUserRepository.GetByOrganizationAsync(org.Id, user.Id);
        if (orgUser == null)
        {
            throw new BadRequestException(new UserNotFoundInOrganizationError().Message);
        }

        return await AcceptOrgUserAsync(orgUser, user, userService);
    }

    public async Task<OrganizationUser> AcceptOrgUserByOrgIdAsync(Guid organizationId, User user, IUserService userService)
    {
        var org = await _organizationRepository.GetByIdAsync(organizationId);
        if (org == null)
        {
            throw new BadRequestException(new OrganizationNotFoundError().Message);
        }

        var orgUser = await _organizationUserRepository.GetByOrganizationAsync(org.Id, user.Id);
        if (orgUser == null)
        {
            throw new BadRequestException(new UserNotFoundInOrganizationError().Message);
        }

        return await AcceptOrgUserAsync(orgUser, user, userService);
    }

    public async Task<OrganizationUser> AcceptOrgUserAsync(OrganizationUser orgUser, User user,
        IUserService userService)
    {
        var org = await _organizationRepository.GetByIdAsync(orgUser.OrganizationId);

        if (orgUser.Status == OrganizationUserStatusType.Revoked)
        {
            throw new BadRequestException(new OrganizationAccessRevoked(org?.DisplayName() ?? string.Empty).Message);
        }

        if (orgUser.Status != OrganizationUserStatusType.Invited)
        {
            throw new BadRequestException(new AlreadyAcceptedError().Message);
        }

        if (orgUser.Type == OrganizationUserType.Owner || orgUser.Type == OrganizationUserType.Admin)
        {
            if (org.PlanType == PlanType.Free)
            {
                var adminCount = await _organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(
                    user.Id);
                if (adminCount > 0)
                {
                    throw new BadRequestException(new FreeOrgAdminLimitError().Message);
                }
            }
        }

        var allOrgUsers = await _organizationUserRepository.GetManyByUserAsync(user.Id);

        var membershipValidationResult = await ValidateMembershipAsync(orgUser, user, allOrgUsers);
        if (membershipValidationResult.AutoConfirmPolicyEnabled)
        {
            await _deleteEmergencyAccessCommand.DeleteAllByUserIdAsync(user.Id);
        }

        orgUser.Status = OrganizationUserStatusType.Accepted;
        orgUser.UserId = user.Id;
        orgUser.Email = null;
        orgUser.ClearInviteReminders();

        await _organizationUserRepository.ReplaceAsync(orgUser);

        await _notifyAdminsOfAcceptedUserCommand.NotifyAsync(org, orgUser, user);

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
                UserIsAMemberOfAnotherOrganization => new UserCannotAcceptInviteMemberOfAnotherOrg().Message,
                UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy => new UserCannotAcceptInviteForbiddenByOtherOrg().Message,
                _ => result.AsError.Message
            };
            throw new BadRequestException(message);
        }

        return result.Request;
    }
}

