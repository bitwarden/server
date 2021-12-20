using System;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.PasswordReset
{
    public class PasswordResetService : IPasswordResetService
    {
        private readonly IPasswordResetAccessPolicies _passwordResetAccessPolicies;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IEventService _eventService;

        public PasswordResetService(
            IPasswordResetAccessPolicies passwordResetAccessPolicies,
            IOrganizationUserRepository organizationUserRepository,
            IEventService eventService
        )
        {
            _passwordResetAccessPolicies = passwordResetAccessPolicies;
            _organizationUserRepository = organizationUserRepository;
            _eventService = eventService;
        }

        public async Task UpdateUserResetPasswordEnrollmentAsync(Guid organizationId, Guid organizationUserId,
            string resetPasswordKey, Guid? callingUserId)
        {
            var orgUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId, organizationUserId);
            CoreHelpers.HandlePermissionResult(
                await _passwordResetAccessPolicies.CanUpdateEnrollmentAsync(organizationId, orgUser, callingUserId, resetPasswordKey)
            );

            orgUser.ResetPasswordKey = resetPasswordKey;
            await _organizationUserRepository.ReplaceAsync(orgUser);
            await _eventService.LogOrganizationUserEventAsync(orgUser,
                resetPasswordKey != null ?
                    EventType.OrganizationUser_ResetPassword_Enroll :
                    EventType.OrganizationUser_ResetPassword_Withdraw);
        }

    }
}
