using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Models.Data.Organizations.Policies;

namespace Bit.Core.Services;

public interface IPolicyService
{
    Task SaveAsync(Policy policy, IUserService userService, IOrganizationService organizationService,
        Guid? savingUserId);

    /// <summary>
    /// Get the combined master password policy options for the specified user.
    /// </summary>
    Task<MasterPasswordPolicyData> GetMasterPasswordPolicyForUserAsync(User user);
    Task<ICollection<OrganizationUserPolicyDetails>> GetPoliciesApplicableToUserAsync(Guid userId, PolicyType policyType, OrganizationUserStatusType minStatus = OrganizationUserStatusType.Accepted);
    Task<bool> AnyPoliciesApplicableToUserAsync(Guid userId, PolicyType policyType, OrganizationUserStatusType minStatus = OrganizationUserStatusType.Accepted);
}
