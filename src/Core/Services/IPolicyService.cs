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

        Task<bool> PolicyAppliesToCurrentUserAsync(PolicyType policyType, Func<Policy, bool> policyFilter);
        Task<bool> PolicyAppliesToCurrentUserAsync(PolicyType policyType, Guid organizationId,
            bool includeInvitedUsers);
    }
}
