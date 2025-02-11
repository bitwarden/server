using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.Services;

public interface IPolicyService
{
    /// <summary>
    /// Get the combined master password policy options for the specified user.
    /// </summary>
    Task<MasterPasswordPolicyData> GetMasterPasswordPolicyForUserAsync(User user);
    [Obsolete("Use IPolicyRequirementQuery.GetAsync instead. You may have to add a new IPolicyRequirement for that query to return.")]
    Task<ICollection<OrganizationUserPolicyDetails>> GetPoliciesApplicableToUserAsync(Guid userId, PolicyType policyType, OrganizationUserStatusType minStatus = OrganizationUserStatusType.Accepted);
    [Obsolete("Use IPolicyRequirementQuery.GetAsync instead. You may have to add a new IPolicyRequirement for that query to return.")]
    Task<bool> AnyPoliciesApplicableToUserAsync(Guid userId, PolicyType policyType, OrganizationUserStatusType minStatus = OrganizationUserStatusType.Accepted);
}
