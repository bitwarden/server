using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery.v2;

public class AdminRecoverAccountValidator(
    IOrganizationRepository organizationRepository,
    IPolicyQuery policyQuery,
    IUserRepository userRepository) : IAdminRecoverAccountValidator
{
    public async Task<ValidationResult<RecoverAccountRequest>> ValidateAsync(RecoverAccountRequest request)
    {
        // At least one action must be requested
        if (!request.ResetMasterPassword && !request.ResetTwoFactor)
        {
            return Invalid(request, new NoActionRequestedError());
        }

        if (request.ResetMasterPassword)
        {
            var hasHashAndKey = !string.IsNullOrEmpty(request.NewMasterPasswordHash) && !string.IsNullOrEmpty(request.Key);
            var hasUnlockAndAuthenticationData = request.AuthenticationData is not null && request.UnlockData is not null;

            if (!hasHashAndKey && !hasUnlockAndAuthenticationData)
            {
                return Invalid(request, new MissingPasswordFieldsError());
            }
        }

        // Org must allow reset password
        var org = await organizationRepository.GetByIdAsync(request.OrgId);
        if (org == null || !org.UseResetPassword)
        {
            return Invalid(request, new OrgDoesNotAllowResetError());
        }

        // Enterprise policy must be enabled
        var resetPasswordPolicy = await policyQuery.RunAsync(request.OrgId, PolicyType.ResetPassword);
        if (!resetPasswordPolicy.Enabled)
        {
            return Invalid(request, new PolicyNotEnabledError());
        }

        // Org User must be confirmed, accepted, or revoked and have a ResetPasswordKey
        var orgUser = request.OrganizationUser;
        if (orgUser == null ||
            (orgUser.Status != OrganizationUserStatusType.Confirmed &&
             orgUser.Status != OrganizationUserStatusType.Revoked &&
             orgUser.Status != OrganizationUserStatusType.Accepted) ||
            orgUser.OrganizationId != request.OrgId ||
            !orgUser.IsEnrolledInAccountRecovery() ||
            !orgUser.UserId.HasValue)
        {
            return Invalid(request, new InvalidOrgUserError());
        }

        // User must exist
        var user = await userRepository.GetByIdAsync(orgUser.UserId.Value);
        if (user == null)
        {
            return Invalid(request, new UserNotFoundError());
        }

        // Key Connector check — only when resetting master password
        if (request.ResetMasterPassword && user.UsesKeyConnector)
        {
            return Invalid(request, new KeyConnectorUserError());
        }

        return Valid(request);
    }
}
