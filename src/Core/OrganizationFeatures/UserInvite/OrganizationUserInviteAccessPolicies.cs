using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Core.AccessPolicies;

namespace Bit.Core.OrganizationFeatures.UserInvite
{
    public class OrganizationUserInviteAccessPolicies : BaseAccessPolicies, IOrganizationUserInviteAccessPolicies
    {
        private IGlobalSettings _globalSettings;
        private ICurrentContext _currentContext;

        public OrganizationUserInviteAccessPolicies(
            IGlobalSettings globalSettings,
            ICurrentContext currentContext
        )
        {
            _globalSettings = globalSettings;
            _currentContext = currentContext;
        }

        public AccessPolicyResult CanScale(Organization organization, int seatsToAdd)
        {
            if (OverrideExists())
            {
                return PermissionOverrides[nameof(CanScale)];
            }

            if (seatsToAdd < 1)
            {
                return Success;
            }

            if (_globalSettings.SelfHosted)
            {
                return Fail("Cannot autoscale on self-hosted instance.");
            }

            if (organization.Seats.HasValue &&
                organization.MaxAutoscaleSeats.HasValue &&
                organization.MaxAutoscaleSeats.Value < organization.Seats.Value + seatsToAdd)
            {
                return Fail($"Cannot invite new users. Seat limit has been reached.");
            }

            return Success;
        }

        public async Task<AccessPolicyResult> UserCanEditUserType(Guid organizationId, OrganizationUserType newType, OrganizationUserType? oldType = null)
        {
            if (await _currentContext.OrganizationOwner(organizationId))
            {
                return Success;
            }

            if (oldType == OrganizationUserType.Owner || newType == OrganizationUserType.Owner)
            {
                return Fail("Only an Owner can configure another Owner's account.");
            }

            if (await _currentContext.OrganizationAdmin(organizationId))
            {
                return Success;
            }

            if (oldType == OrganizationUserType.Custom || newType == OrganizationUserType.Custom)
            {
                return Fail("Only Owners and Admins can configure Custom accounts.");
            }

            if (!await _currentContext.ManageUsers(organizationId))
            {
                return Fail("Your account does not have permission to manage users.");
            }

            // TODO: this appears broken, only testing Admin, not Owner
            if (oldType == OrganizationUserType.Admin || newType == OrganizationUserType.Admin)
            {
                return Fail("Custom users can not manage Admins or Owners.");
            }

            return Success;
        }
    }
}
