using System;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface IPolicyService
    {
        Task SaveAsync(Policy policy, IUserService userService, IOrganizationService organizationService,
            Guid? savingUserId);

        Task<bool> PolicyAppliesToUserAsync(PolicyType policyType, Guid userId, Func<Policy, bool> policyFilter);
        Task<bool> PolicyAppliesToUserAsync(PolicyType policyType, Guid userId, Guid organizationId,
            bool includeInvitedUsers);
    }
}
