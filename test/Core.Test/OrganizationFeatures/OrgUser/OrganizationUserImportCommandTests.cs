using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using Bit.Core.AccessPolicies;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.Mail;
using Bit.Core.OrganizationFeatures.OrgUser;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Services.OrganizationServices.UserInvite;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using NSubstitute;
using Xunit;
using Organization = Bit.Core.Models.Table.Organization;
using OrganizationUser = Bit.Core.Models.Table.OrganizationUser;

namespace Bit.Core.Test.OrganizationFeatures.OrgUser
{
    public class OrganizationUserImportCommandTests
    {
        [Theory, PaidOrganizationAutoData]
        public async Task OrgImportCreateNewUsers(SutProvider<OrganizationUserImportCommand> sutProvider, Guid userId,
            Organization org, List<OrganizationUserUserDetails> existingUsers, List<ImportedOrganizationUser> newUsers)
        {
            org.UseDirectory = true;
            org.Seats = 10;
            newUsers.Add(new ImportedOrganizationUser
            {
                Email = existingUsers.First().Email,
                ExternalId = existingUsers.First().ExternalId
            });
            var expectedNewUsersCount = newUsers.Count - 1;

            existingUsers.First().Type = OrganizationUserType.Owner;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
            sutProvider.GetDependency<IOrganizationUserImportAccessPolicies>().CanImport(org).Returns(AccessPolicyResult.Success);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id)
                .Returns(existingUsers);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetCountByOrganizationIdAsync(org.Id)
                .Returns(existingUsers.Count);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByOrganizationAsync(org.Id, OrganizationUserType.Owner)
                .Returns(existingUsers.Select(u => new OrganizationUser { Status = OrganizationUserStatusType.Confirmed, Type = OrganizationUserType.Owner, Id = u.Id }).ToList());

            sutProvider.GetDependency<IOrganizationUserInviteService>().InviteUsersAsync(default, default, default)
                .ReturnsForAnyArgs(sutProvider.Fixture.CreateMany<OrganizationUser>(expectedNewUsersCount).ToList());

            await sutProvider.Sut.ImportAsync(org.Id, userId, null, newUsers, null, false);

            await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
            await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
                .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.Count() == 0));
            await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
                .CreateAsync(default);

            // Create new users
            await sutProvider.GetDependency<IOrganizationUserInviteService>().Received(1)
                .InviteUsersAsync(org, Arg.Is<IEnumerable<(OrganizationUserInviteData, string)>>(i => i.Count() == expectedNewUsersCount), Arg.Any<HashSet<string>>());

            // Send Invites
            await sutProvider.GetDependency<IOrganizationUserMailer>().Received(1)
                .SendInvitesAsync(Arg.Is<IEnumerable<(OrganizationUser, ExpiringToken)>>(i => i.Count() == expectedNewUsersCount), org);

            // Send events
            await sutProvider.GetDependency<IEventService>().Received(1)
                .LogOrganizationUserEventsAsync(Arg.Is<IEnumerable<(OrganizationUser, EventType, DateTime?)>>(events =>
                events.Count() == expectedNewUsersCount));
            await sutProvider.GetDependency<IReferenceEventService>().Received(1)
                .RaiseEventAsync(Arg.Is<ReferenceEvent>(referenceEvent =>
                referenceEvent.Type == ReferenceEventType.InvitedUsers && referenceEvent.Id == org.Id &&
                referenceEvent.Users == expectedNewUsersCount));
        }

        [Theory, PaidOrganizationAutoData]
        public async Task OrgImportCreateNewUsersAndMarryExistingUser(SutProvider<OrganizationUserImportCommand> sutProvider,
            Guid userId, Organization org, List<OrganizationUserUserDetails> existingUsers,
            List<ImportedOrganizationUser> newUsers)
        {
            org.UseDirectory = true;
            org.Seats = newUsers.Count + existingUsers.Count + 1;
            var reInvitedUser = existingUsers.First();
            reInvitedUser.ExternalId = null;
            newUsers.Add(new ImportedOrganizationUser
            {
                Email = reInvitedUser.Email,
                ExternalId = reInvitedUser.Email,
            });
            var expectedNewUsersCount = newUsers.Count - 1;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
            sutProvider.GetDependency<IOrganizationUserImportAccessPolicies>().CanImport(org).Returns(AccessPolicyResult.Success);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id)
                .Returns(existingUsers);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetCountByOrganizationIdAsync(org.Id)
                .Returns(existingUsers.Count);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(reInvitedUser.Id)
                .Returns(new OrganizationUser { Id = reInvitedUser.Id });
            sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByOrganizationAsync(org.Id, OrganizationUserType.Owner)
                .Returns(existingUsers.Select(u => new OrganizationUser { Status = OrganizationUserStatusType.Confirmed, Type = OrganizationUserType.Owner, Id = u.Id }).ToList());

            sutProvider.GetDependency<IOrganizationUserInviteService>().InviteUsersAsync(default, default, default)
                .ReturnsForAnyArgs(sutProvider.Fixture.CreateMany<OrganizationUser>(expectedNewUsersCount).ToList());


            await sutProvider.Sut.ImportAsync(org.Id, userId, null, newUsers, null, false);

            await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
            await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
                .CreateAsync(default);
            await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
                .CreateAsync(default, default);

            // Upserted existing user
            await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
                .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.Count() == 1));

            // Create new users
            await sutProvider.GetDependency<IOrganizationUserInviteService>().Received(1)
                .InviteUsersAsync(org, Arg.Is<IEnumerable<(OrganizationUserInviteData, string)>>(i => i.Count() == expectedNewUsersCount), Arg.Any<HashSet<string>>());

            // Send Invites
            await sutProvider.GetDependency<IOrganizationUserMailer>().Received(1)
                .SendInvitesAsync(Arg.Is<IEnumerable<(OrganizationUser, ExpiringToken)>>(messages => messages.Count() == expectedNewUsersCount), org);

            // Sent events
            await sutProvider.GetDependency<IEventService>().Received(1)
                .LogOrganizationUserEventsAsync(Arg.Is<IEnumerable<(OrganizationUser, EventType, DateTime?)>>(events =>
                events.Where(e => e.Item2 == EventType.OrganizationUser_Invited).Count() == expectedNewUsersCount));
            await sutProvider.GetDependency<IReferenceEventService>().Received(1)
                .RaiseEventAsync(Arg.Is<ReferenceEvent>(referenceEvent =>
                referenceEvent.Type == ReferenceEventType.InvitedUsers && referenceEvent.Id == org.Id &&
                referenceEvent.Users == expectedNewUsersCount));
        }

    }
}
