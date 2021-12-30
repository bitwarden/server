using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrgUser
{
    public class OrganizationUserAccessPolicies : BaseAccessPolicies, IOrganizationUserAccessPolicies
    {
        readonly ICurrentContext _currentContext;
        readonly IOrganizationUserRepository _organizationUserRepository;
        readonly IOrganizationService _organizationService;

        public OrganizationUserAccessPolicies(
            ICurrentContext currentContext,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationService organizationService
        )
        {
            _currentContext = currentContext;
            _organizationUserRepository = organizationUserRepository;
            _organizationService = organizationService;
        }

        public async Task<AccessPolicyResult> CanSaveAsync(OrganizationUser orgUser, Guid? savingUserId)
        {
            if (orgUser.Id.Equals(default))
            {
                return Fail("Invite the user first.");
            }

            // TODO: can probably remove this check, this is doing a reference equals and will never be true
            var originalUser = await _organizationUserRepository.GetByIdAsync(orgUser.Id);
            if (orgUser.Equals(originalUser))
            {
                return Fail("Please make changes before saving.");
            }

            var currentResult = Success;
            if (savingUserId.HasValue)
            {
                currentResult = currentResult.And(
                    await UserCanEditUserTypeAsync(orgUser.OrganizationId, orgUser.Type, originalUser.Type)
                );
            }

            return await currentResult.AndAsync(() => OrganizationCanLoseOwnerAsync(orgUser.OrganizationId, orgUser));
        }

        public async Task<AccessPolicyResult> CanDeleteUserAsync(Guid organizationId, OrganizationUser orgUser,
            Guid? deletingUserId)
        {
            if (orgUser == null || orgUser.OrganizationId != organizationId)
            {
                return Fail("User not valid.");
            }

            if (deletingUserId.HasValue && orgUser.UserId == deletingUserId.Value)
            {
                return Fail("You cannot remove yourself.");
            }

            if (orgUser.Type == OrganizationUserType.Owner && deletingUserId.HasValue &&
                !await _currentContext.OrganizationOwner(organizationId))
            {
                return Fail("Only owners can delete other owners.");
            }

            return await OrganizationCanLoseOwnerAsync(orgUser.OrganizationId, orgUser);
        }

        public async Task<AccessPolicyResult> CanDeleteManyUsersAsync(Guid organizationId,
            IEnumerable<OrganizationUser> orgUsers, Guid? deletingUserId)
        {
            if (!orgUsers.Any())
            {
                return Fail("Users invalid.");
            }

            return Success.And(await OrganizationCanLoseOwnerAsync(organizationId, orgUsers.ToArray()));
        }

        public async Task<AccessPolicyResult> CanSelfDeleteUserAsync(OrganizationUser orgUser)
        {
            if (orgUser == null)
            {
                return Fail();
            }

            return await OrganizationCanLoseOwnerAsync(orgUser.OrganizationId, orgUser);
        }

        protected async Task<AccessPolicyResult> OrganizationCanLoseOwnerAsync(Guid organizationId, params OrganizationUser[] orgUsers)
        {
            if (!await _organizationService.HasConfirmedOwnersExceptAsync(organizationId, orgUsers.Select(u => u.Id)))
            {
                return Fail("Organization must have at least one confirmed owner.");
            }

            return Success;
        }

        public async Task<AccessPolicyResult> UserCanEditUserTypeAsync(Guid organizationId, OrganizationUserType newType, OrganizationUserType? oldType = null)
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
