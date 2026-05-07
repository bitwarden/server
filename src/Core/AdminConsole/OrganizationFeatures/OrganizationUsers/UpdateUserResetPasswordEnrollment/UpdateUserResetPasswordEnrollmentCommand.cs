using System.Text.Json;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUserResetPasswordEnrollment;

public class UpdateUserResetPasswordEnrollmentCommand : IUpdateUserResetPasswordEnrollmentCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IPolicyQuery _policyQuery;
    private readonly IEventService _eventService;

    public UpdateUserResetPasswordEnrollmentCommand(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyQuery policyQuery,
        IEventService eventService)
    {
        _organizationUserRepository = organizationUserRepository;
        _organizationRepository = organizationRepository;
        _policyQuery = policyQuery;
        _eventService = eventService;
    }

    public async Task UpdateUserResetPasswordEnrollmentAsync(Guid organizationId, Guid userId, string? resetPasswordKey,
        Guid? callingUserId)
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
        // Todo: Cannot use PolicyRequirements until PM-34092 is complete
        var resetPasswordPolicy = await _policyQuery.RunAsync(organizationId, PolicyType.ResetPassword);
        if (!resetPasswordPolicy.Enabled)
        {
            throw new BadRequestException("Organization does not have the password reset policy enabled.");
        }

        // Block the user from withdrawal if auto enrollment is enabled
        if (resetPasswordKey == null && resetPasswordPolicy.Data != null)
        {
            var data = JsonSerializer.Deserialize<ResetPasswordDataModel>(resetPasswordPolicy.Data,
                JsonHelpers.IgnoreCase);

            if (data?.AutoEnrollEnabled ?? false)
            {
                throw new BadRequestException(
                    "Due to an Enterprise Policy, you are not allowed to withdraw from account recovery.");
            }
        }

        orgUser.ResetPasswordKey = resetPasswordKey;
        await _organizationUserRepository.ReplaceAsync(orgUser);
        await _eventService.LogOrganizationUserEventAsync(orgUser,
            resetPasswordKey != null
                ? EventType.OrganizationUser_ResetPassword_Enroll
                : EventType.OrganizationUser_ResetPassword_Withdraw);
    }
}
