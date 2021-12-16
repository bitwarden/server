using System;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Newtonsoft.Json;

namespace Bit.Core.OrganizationFeatures.PasswordReset
{
    public class PasswordResetAccessPolicies : BaseAccessPolicies, IPasswordResetAccessPolicies
    {
        readonly IOrganizationRepository _organizationRepository;
        readonly IPolicyRepository _policyRepository;

        public PasswordResetAccessPolicies(
            IOrganizationRepository organizationRepository,
            IPolicyRepository policyRepository
        )
        {
            _organizationRepository = organizationRepository;
            _policyRepository = policyRepository;
        }

        public async Task<AccessPolicyResult> CanUpdateEnrollmentAsync(Guid organizationId, OrganizationUser orgUser,
            Guid? callingUserId, string resetPasswordKey)
        {
            // Org User must be the same as the calling user and the organization ID associated with the user must match passed org ID
            if (!callingUserId.HasValue || orgUser == null || orgUser.UserId != callingUserId.Value ||
                orgUser.OrganizationId != organizationId)
            {
                return Fail("User not valid.");
            }

            // Make sure the organization has the ability to use password reset
            var org = await _organizationRepository.GetByIdAsync(organizationId);
            if (org == null || !org.UseResetPassword)
            {
                return Fail("Organization does not allow password reset enrollment.");
            }

            // Make sure the organization has the policy enabled
            var resetPasswordPolicy =
                await _policyRepository.GetByOrganizationIdTypeAsync(organizationId, PolicyType.ResetPassword);
            if (resetPasswordPolicy == null || !resetPasswordPolicy.Enabled)
            {
                return Fail("Organization does not have the password reset policy enabled.");
            }

            // Block the user from withdrawal if auto enrollment is enabled
            if (resetPasswordKey == null && resetPasswordPolicy.Data != null)
            {
                var data = JsonConvert.DeserializeObject<ResetPasswordDataModel>(resetPasswordPolicy.Data);

                if (data?.AutoEnrollEnabled ?? false)
                {
                    return Fail("Due to an Enterprise Policy, you are not allowed to withdraw from Password Reset.");
                }
            }

            return Success;
        }
    }
}
