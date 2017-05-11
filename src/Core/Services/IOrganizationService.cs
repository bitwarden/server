using System.Threading.Tasks;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;
using System;
using System.Collections.Generic;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.Services
{
    public interface IOrganizationService
    {
        Task<OrganizationBilling> GetBillingAsync(Organization organization);
        Task ReplacePaymentMethodAsync(Guid organizationId, string paymentToken);
        Task CancelSubscriptionAsync(Guid organizationId, bool endOfPeriod = false);
        Task ReinstateSubscriptionAsync(Guid organizationId);
        Task UpgradePlanAsync(Guid organizationId, PlanType plan, int additionalSeats);
        Task AdjustSeatsAsync(Guid organizationId, int seatAdjustment);
        Task<Tuple<Organization, OrganizationUser>> SignUpAsync(OrganizationSignup organizationSignup);
        Task DeleteAsync(Organization organization);
        Task DisableAsync(Guid organizationId);
        Task EnableAsync(Guid organizationId);
        Task UpdateAsync(Organization organization, bool updateBilling = false);
        Task<OrganizationUser> InviteUserAsync(Guid organizationId, Guid invitingUserId, string email,
            OrganizationUserType type, bool accessAll, IEnumerable<SelectionReadOnly> collections);
        Task ResendInviteAsync(Guid organizationId, Guid invitingUserId, Guid organizationUserId);
        Task<OrganizationUser> AcceptUserAsync(Guid organizationUserId, User user, string token);
        Task<OrganizationUser> ConfirmUserAsync(Guid organizationId, Guid organizationUserId, string key, Guid confirmingUserId);
        Task SaveUserAsync(OrganizationUser user, Guid savingUserId, IEnumerable<SelectionReadOnly> collections);
        Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid deletingUserId);
        Task DeleteUserAsync(Guid organizationId, Guid userId);
    }
}
