using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrgUser;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Xunit;
using Organization = Bit.Core.Models.Table.Organization;
using OrganizationUser = Bit.Core.Models.Table.OrganizationUser;


namespace Bit.Core.Test.OrganizationFeatures.OrgUser
{
    [SutProviderCustomize]
    public class OrganizationUserAccessPoliciesTests
    {
        [Theory, BitAutoData]
        public async Task SaveUser_NoUserId_Throws(OrganizationUser orgUser, Guid? savingUserId,
            SutProvider<OrganizationUserAccessPolicies> sutProvider)
        {
            orgUser.Id = default(Guid);

            var result = await sutProvider.Sut.CanSaveAsync(orgUser, savingUserId);

            Assert.Equal(AccessPolicyResult.Fail("Invite the user first."), result);
        }

        [Theory, BitAutoData]
        public async Task SaveUser_NoChangeToData_Throws(OrganizationUser orgUser, Guid? savingUserId,
            SutProvider<OrganizationUserAccessPolicies> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(orgUser.Id).Returns(orgUser);

            var result = await sutProvider.Sut.CanSaveAsync(orgUser, savingUserId);

            Assert.Equal(AccessPolicyResult.Fail("Please make changes before saving."), result);
        }

        [Theory, BitAutoData]
        public async Task SaveUser_Passes(OrganizationUser oldOrgUser, OrganizationUser newOrgUser,
            IEnumerable<SelectionReadOnly> collections,
            [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser savingUser,
            SutProvider<OrganizationUserAccessPolicies> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var organizationService = sutProvider.GetDependency<IOrganizationService>();
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(default).ReturnsForAnyArgs(true);

            newOrgUser.Id = oldOrgUser.Id;
            newOrgUser.UserId = oldOrgUser.UserId;
            newOrgUser.OrganizationId = savingUser.OrganizationId = oldOrgUser.OrganizationId;
            organizationUserRepository.GetByIdAsync(oldOrgUser.Id).Returns(oldOrgUser);
            organizationService.HasConfirmedOwnersExceptAsync(default, default)
                .ReturnsForAnyArgs(true);

            var result = await sutProvider.Sut.CanSaveAsync(newOrgUser, savingUser.UserId);

            Assert.Equal(AccessPolicyResult.Success, result);
        }

        [Theory, BitAutoData]
        public async Task DeleteUser_InvalidUser(OrganizationUser organizationUser, Guid deletingUserId,
            SutProvider<OrganizationUserAccessPolicies> sutProvider)
        {
            var organizationId = Guid.NewGuid();

            sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(organizationUser.Id).Returns(organizationUser);

            var result =
                await sutProvider.Sut.CanDeleteUserAsync(organizationId, organizationUser, deletingUserId);

            Assert.Equal(AccessPolicyResult.Fail("User not valid."), result);
        }

        [Theory, BitAutoData]
        public async Task DeleteUser_RemoveYourself(OrganizationUser deletingUser, SutProvider<OrganizationUserAccessPolicies> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(deletingUser.Id).Returns(deletingUser);

            var result =
                await sutProvider.Sut.CanDeleteUserAsync(deletingUser.OrganizationId, deletingUser,
                    deletingUser.UserId);

            Assert.Equal(AccessPolicyResult.Fail("You cannot remove yourself."), result);
        }

        [Theory, BitAutoData]
        public async Task DeleteUser_NonOwnerRemoveOwner(
            [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser organizationUser,
            [OrganizationUser(type: OrganizationUserType.Admin)] OrganizationUser deletingUser,
            SutProvider<OrganizationUserAccessPolicies> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationService>()
                .HasConfirmedOwnersExceptAsync(deletingUser.OrganizationId, new[] { organizationUser.Id }).Returns(false);

            var result = await sutProvider.Sut.CanDeleteUserAsync(organizationUser.OrganizationId, organizationUser,
                deletingUser.UserId);

            Assert.Equal(AccessPolicyResult.Fail("Only owners can delete other owners."), result);
        }

        [Theory, BitAutoData]
        public async Task DeleteUser_LastOwner(
            [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser organizationUser,
            OrganizationUser deletingUser,
            SutProvider<OrganizationUserAccessPolicies> sutProvider)
        {
            organizationUser.OrganizationId = deletingUser.OrganizationId;

            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(default).ReturnsForAnyArgs(true);
            sutProvider.GetDependency<IOrganizationService>()
                .HasConfirmedOwnersExceptAsync(default, default).ReturnsForAnyArgs(false);

            var result = await sutProvider.Sut.CanDeleteUserAsync(deletingUser.OrganizationId, organizationUser,
                deletingUser.UserId);

            Assert.Equal(AccessPolicyResult.Fail("Organization must have at least one confirmed owner."), result);
        }

        [Theory, BitAutoData]
        public async Task DeleteUser_Success(
            OrganizationUser organizationUser,
            [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser deletingUser,
            SutProvider<OrganizationUserAccessPolicies> sutProvider)
        {
            organizationUser.OrganizationId = deletingUser.OrganizationId;

            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(default).ReturnsForAnyArgs(true);
            sutProvider.GetDependency<IOrganizationService>()
                .HasConfirmedOwnersExceptAsync(default, default).ReturnsForAnyArgs(true);

            var result = await sutProvider.Sut.CanDeleteUserAsync(deletingUser.OrganizationId, organizationUser,
                deletingUser.UserId);

            Assert.Equal(AccessPolicyResult.Success, result);
        }

        [Theory, BitAutoData]
        public async Task DeleteUsers_EmptyList(SutProvider<OrganizationUserAccessPolicies> sutProvider)
        {
            var result =
                await sutProvider.Sut.CanDeleteManyUsersAsync(Guid.NewGuid(), Array.Empty<OrganizationUser>(),
                    Guid.NewGuid());

            Assert.Equal(AccessPolicyResult.Fail("Users invalid."), result);
        }

        [Theory, BitAutoData]
        public async Task DeleteUsers_LastOwner(
            [OrganizationUser(status: OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser orgUser,
            SutProvider<OrganizationUserAccessPolicies> sutProvider)
        {
            var organizationUsers = new[] { orgUser };

            sutProvider.GetDependency<IOrganizationService>().HasConfirmedOwnersExceptAsync(default, default)
                .ReturnsForAnyArgs(false);

            var result =
                await sutProvider.Sut.CanDeleteManyUsersAsync(orgUser.OrganizationId, organizationUsers, null);

            Assert.Equal(AccessPolicyResult.Fail("Organization must have at least one confirmed owner."), result);
        }

        [Theory, BitAutoData]
        public async Task DeleteUsers_Success(
            [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser deletingUser,
            [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser orgUser1, OrganizationUser orgUser2,
            SutProvider<OrganizationUserAccessPolicies> sutProvider)
        {
            orgUser1.OrganizationId = orgUser2.OrganizationId = deletingUser.OrganizationId;
            var organizationUsers = new[] { orgUser1, orgUser2 };

            sutProvider.GetDependency<IOrganizationService>().HasConfirmedOwnersExceptAsync(default, default)
                .ReturnsForAnyArgs(true);

            var result =
                await sutProvider.Sut.CanDeleteManyUsersAsync(orgUser1.OrganizationId, organizationUsers, null);

            Assert.Equal(AccessPolicyResult.Success, result);
        }

    }
}
