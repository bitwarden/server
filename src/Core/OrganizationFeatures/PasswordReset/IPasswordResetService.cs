using System;
using System.Threading.Tasks;

namespace Bit.Core.OrganizationFeatures.PasswordReset
{
    public interface IPasswordResetService
    {
        Task UpdateUserResetPasswordEnrollmentAsync(Guid organizationId, Guid organizationUserId,
            string resetPasswordKey, Guid? callingUserId);
    }
}
