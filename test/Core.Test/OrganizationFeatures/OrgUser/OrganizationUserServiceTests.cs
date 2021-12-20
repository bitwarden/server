using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.OrganizationFeatures.OrgUser;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using NSubstitute;
using Xunit;
using OrganizationUser = Bit.Core.Models.Table.OrganizationUser;


namespace Bit.Core.Test.OrganizationFeatures.OrgUser
{
    [SutProviderCustomize]
    public class OrganizationUserServiceTests
    {
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveUser_Passes(
            OrganizationUser orgUser,
            IEnumerable<SelectionReadOnly> collections, Guid savingUserId,
            SutProvider<OrganizationUserService> sutProvider)
        {
            var accessPolicies = sutProvider.GetDependency<IOrganizationUserAccessPolicies>();
            var orgUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            accessPolicies.CanSaveAsync(default, default)
                .ReturnsForAnyArgs(AccessPolicyResult.Success);

            await sutProvider.Sut.SaveUserAsync(orgUser, savingUserId, collections);

            await accessPolicies.Received(1).CanSaveAsync(orgUser, savingUserId);
            if (orgUser.AccessAll)
            {
                await orgUserRepository.Received(1)
                    .ReplaceAsync(orgUser, Arg.Is<List<SelectionReadOnly>>(l => l.Count == 0));
            }
            else
            {
                await orgUserRepository.Received(1).ReplaceAsync(orgUser, collections);
            }

            await sutProvider.GetDependency<IEventService>().Received(1)
                .LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Updated);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task DeleteUser_Success(
             OrganizationUser organizationUser, Guid deletingUserId,
             SutProvider<OrganizationUserService> sutProvider)
        {
            var accessPolicies = sutProvider.GetDependency<IOrganizationUserAccessPolicies>();
            var orgUserRespository = sutProvider.GetDependency<IOrganizationUserRepository>();

            accessPolicies.CanDeleteUserAsync(default, default, default).ReturnsForAnyArgs(AccessPolicyResult.Success);
            orgUserRespository.GetByIdAsync(organizationUser.Id).Returns(organizationUser);

            await sutProvider.Sut.DeleteUserAsync(organizationUser.OrganizationId, organizationUser.Id, deletingUserId);

            await accessPolicies.Received(1)
                .CanDeleteUserAsync(organizationUser.OrganizationId, organizationUser, deletingUserId);
            await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).DeleteAsync(organizationUser);
            await sutProvider.GetDependency<IEventService>().Received(1)
                .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed);
            if (organizationUser.UserId.HasValue)
            {
                await sutProvider.GetDependency<IPushRegistrationService>().ReceivedWithAnyArgs(1)
                    .DeleteUserRegistrationOrganizationAsync(default, default);
                await sutProvider.GetDependency<IPushNotificationService>().ReceivedWithAnyArgs(1)
                    .PushSyncOrgKeysAsync(default);
            }
        }

        [Theory, BitAutoData]
        public async Task DeleteUsersAsync_Passes(Organization org, List<OrganizationUser> orgUsers, Guid deletingUserId,
           SutProvider<OrganizationUserService> sutProvider)
        {
            orgUsers.ForEach(u => u.OrganizationId = org.Id);
            var orgUserIds = orgUsers.Select(u => u.Id);

            var accessPolicies = sutProvider.GetDependency<IOrganizationUserAccessPolicies>();
            var orgUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

            orgUserRepository.GetManyAsync(orgUserIds).Returns(orgUsers);
            accessPolicies.CanDeleteManyUsersAsync(default, default, default)
                .ReturnsForAnyArgs(AccessPolicyResult.Success);
            accessPolicies.CanDeleteUserAsync(default, default, default).ReturnsForAnyArgs(AccessPolicyResult.Success);

            var result =
                await sutProvider.Sut.DeleteUsersAsync(org.Id, orgUserIds, deletingUserId);

            await accessPolicies.ReceivedWithAnyArgs(1).CanDeleteManyUsersAsync(default, default, default);
            await accessPolicies.ReceivedWithAnyArgs(orgUsers.Count).CanDeleteUserAsync(default, default, default);
            var numOrgUsersWithUserId = orgUsers.Count(u => u.UserId.HasValue);
            await sutProvider.GetDependency<IPushRegistrationService>().ReceivedWithAnyArgs(numOrgUsersWithUserId)
                .DeleteUserRegistrationOrganizationAsync(default, default);
            await sutProvider.GetDependency<IPushNotificationService>().ReceivedWithAnyArgs(numOrgUsersWithUserId)
                .PushSyncOrgKeysAsync(default);
            await sutProvider.GetDependency<IEventService>().ReceivedWithAnyArgs(1).LogOrganizationUserEventsAsync(default);
            await orgUserRepository.Received(1).DeleteManyAsync(Arg.Is<IEnumerable<Guid>>(l =>
                l.Count() == orgUsers.Count && !l.Except(orgUserIds).Any()));
        }
    }
}
