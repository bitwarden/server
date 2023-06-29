using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.Policies;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers;

public class UpdateUserResetPasswordEnrollmentCommand : IUpdateUserResetPasswordEnrollmentCommand
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IEventService _eventService;
    private readonly IPolicyRepository _policyRepository;

    public UpdateUserResetPasswordEnrollmentCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IEventService eventService,
        IPolicyRepository policyRepository)
    {
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _eventService = eventService;
        _policyRepository = policyRepository;
    }

    public async Task UpdateAsync(Guid organizationId, Guid userId, string resetPasswordKey, Guid? callingUserId)
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
        if (resetPasswordKey == null && resetPasswordPolicy.Data != null)
        {
            var data = JsonSerializer.Deserialize<ResetPasswordDataModel>(resetPasswordPolicy.Data, JsonHelpers.IgnoreCase);

            if (data?.AutoEnrollEnabled ?? false)
            {
                throw new BadRequestException("Due to an Enterprise Policy, you are not allowed to withdraw from Password Reset.");
            }
        }

        orgUser.ResetPasswordKey = resetPasswordKey;
        await _organizationUserRepository.ReplaceAsync(orgUser);
        await _eventService.LogOrganizationUserEventAsync(orgUser, resetPasswordKey != null ?
            EventType.OrganizationUser_ResetPassword_Enroll : EventType.OrganizationUser_ResetPassword_Withdraw);
    }
}

