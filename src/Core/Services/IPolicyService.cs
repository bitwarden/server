using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.Services;

public interface IPolicyService
{
    Task SaveAsync(Policy policy, IUserService userService, IOrganizationService organizationService,
        Guid? savingUserId);
    Task<ICollection<OrganizationUserPolicyDetails>> GetPoliciesApplicableToUserAsync(Guid userId, PolicyType policyType,
        OrganizationUserType minUserType = OrganizationUserType.User, OrganizationUserStatusType minStatus = OrganizationUserStatusType.Accepted);
    Task<bool> AnyPoliciesApplicableToUserAsync(Guid userId, PolicyType policyType,
        OrganizationUserType minUserType = OrganizationUserType.User, OrganizationUserStatusType minStatus = OrganizationUserStatusType.Accepted);
}
