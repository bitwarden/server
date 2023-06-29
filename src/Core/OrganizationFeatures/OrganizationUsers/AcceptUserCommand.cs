using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers;

public class AcceptUserCommand : IAcceptUserCommand
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IDataProtector _dataProtector;
    private readonly IMailService _mailService;
    private readonly IPolicyService _policyService;
    private readonly IGlobalSettings _globalSettings;
    private readonly IUserService _userService;

    public AcceptUserCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IDataProtector dataProtector,
        IMailService mailService,
        IPolicyService policyService,
        IGlobalSettings globalSettings,
        IUserService userService)
    {
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _dataProtector = dataProtector;
        _mailService = mailService;
        _policyService = policyService;
        _globalSettings = globalSettings;
        _userService = userService;
    }

    public async Task<OrganizationUser> AcceptAsync(Guid organizationUserId, User user, string token)
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

        return await AcceptAsync(orgUser, user);
    }

    public async Task<OrganizationUser> AcceptAsync(string orgIdentifier, User user)
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

        return await AcceptAsync(orgUser, user);
    }

    public async Task<OrganizationUser> AcceptAsync(Guid organizationId, User user)
    {
        var org = await _organizationRepository.GetByIdAsync(organizationId);
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

        return await AcceptAsync(orgUser, user);
    }

    private async Task<OrganizationUser> AcceptAsync(OrganizationUser orgUser, User user)
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
        if (!await _userService.TwoFactorIsEnabledAsync(user))
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
}

