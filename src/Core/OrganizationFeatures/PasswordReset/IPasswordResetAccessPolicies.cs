using System;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.PasswordReset
{
    public interface IPasswordResetAccessPolicies
    {
        Task<AccessPolicyResult> CanUpdateEnrollmentAsync(Guid organizationId, OrganizationUser orgUser,
            Guid? callingUserId, string resetPasswordKey);
    }
}
