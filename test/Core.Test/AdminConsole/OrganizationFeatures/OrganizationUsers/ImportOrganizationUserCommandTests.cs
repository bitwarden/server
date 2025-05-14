using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.Utilities.Commands;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Tokens;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Fakes;
using NSubstitute;
using Xunit;
using Organization = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationUsers;

public class ImportOrganizationUserCommandTests
{

    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory = new FakeDataProtectorTokenFactory<OrgUserInviteTokenable>();

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task OrgImportCreateNewUsers(
            SutProvider<ImportOrganizationUserCommand> sutProvider,
            Organization org,
            List<OrganizationUserUserDetails> existingUsers,
            List<ImportedOrganizationUser> newUsers,
            List<ImportedGroup> newGroups)
    {
        SetupOrganizationConfigForImport(sutProvider, org, existingUsers, newUsers);

        var expectedNewUsersCount = newUsers.Count - 1;
        var invitedOrganizationUsers = new List<OrganizationUser>();
        var invites = new List<OrganizationUserInviteCommandModel>();

        foreach (var u in newUsers)
        {
            invitedOrganizationUsers.Add(new OrganizationUser { Email = u.Email + "@test.com", ExternalId = u.ExternalId });
            invites.Add(new OrganizationUserInviteCommandModel(u.Email + "@test.com", u.ExternalId));
        }

        var inviteCommandModel = new InviteOrganizationUsersRequest(invites.ToArray(), new InviteOrganization(org, null), Guid.Empty, DateTimeOffset.UtcNow);

        newUsers.Add(new ImportedOrganizationUser
        {
            Email = existingUsers.First().Email,
            ExternalId = existingUsers.First().ExternalId
        });

        existingUsers.First().Type = OrganizationUserType.Owner;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IPaymentService>().HasSecretsManagerStandalone(org).Returns(false);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id).Returns(existingUsers);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetCountByOrganizationIdAsync(org.Id).Returns(existingUsers.Count);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(org.Id).Returns(true);
        // Use the arranged command model
        sutProvider.GetDependency<IInviteOrganizationUsersCommand>().InviteImportedOrganizationUsersAsync(inviteCommandModel, org.Id)
            // assert against this returned CommandResult response
            .Returns(new Success<InviteOrganizationUsersResponse>(new InviteOrganizationUsersResponse(invitedOrganizationUsers, org.Id)));

        await sutProvider.Sut.ImportAsync(org.Id, newGroups, newUsers, new List<string>(), false, EventSystemUser.PublicApi);

        await sutProvider.GetDependency<IInviteOrganizationUsersCommand>().Received(1)
            .InviteImportedOrganizationUsersAsync(Arg.Is<InviteOrganizationUsersRequest>(
                        // These are the invites that should get populated from the CommandResult response above
                        request => request.Invites.Count() == 0
                        ), org.Id);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => !users.Any()));
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default);

        // Send events
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUserUserDetails, EventType, EventSystemUser, DateTime?)>>());
        await sutProvider.GetDependency<IReferenceEventService>().Received(1)
            .RaiseEventAsync(Arg.Any<ReferenceEvent>());
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task OrgImportCreateNewUsersAndMarryExistingUser(
            SutProvider<ImportOrganizationUserCommand> sutProvider,
            Organization org,
            List<OrganizationUserUserDetails> existingUsers,
            List<ImportedOrganizationUser> newUsers,
            List<ImportedGroup> newGroups)
    {
        SetupOrganizationConfigForImport(sutProvider, org, existingUsers, newUsers);

        var reInvitedUser = existingUsers.First();
        reInvitedUser.ExternalId = null;
        newUsers.Add(new ImportedOrganizationUser
        {
            Email = reInvitedUser.Email,
            ExternalId = reInvitedUser.Email,
        });

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id).Returns(existingUsers);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetCountByOrganizationIdAsync(org.Id).Returns(existingUsers.Count);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(reInvitedUser.Id).Returns(new OrganizationUser { Id = reInvitedUser.Id });
        sutProvider.GetDependency<IInviteOrganizationUsersCommand>().InviteImportedOrganizationUsersAsync(Arg.Any<InviteOrganizationUsersRequest>(), org.Id)
            .Returns(new Success<InviteOrganizationUsersResponse>(new InviteOrganizationUsersResponse(org.Id)));


        await sutProvider.Sut.ImportAsync(org.Id, newGroups, newUsers, new List<string>(), false, EventSystemUser.PublicApi);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default, default);

        // Upserted existing user
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.Count() == 1));

        await sutProvider.GetDependency<IInviteOrganizationUsersCommand>().Received(1)
            .InviteImportedOrganizationUsersAsync(Arg.Any<InviteOrganizationUsersRequest>(), org.Id);

        // Send events
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUserUserDetails, EventType, EventSystemUser, DateTime?)>>());
        await sutProvider.GetDependency<IReferenceEventService>().Received(1)
            .RaiseEventAsync(Arg.Any<ReferenceEvent>());

    }

    private void SetupOrganizationConfigForImport(
            SutProvider<ImportOrganizationUserCommand> sutProvider,
            Organization org,
            List<OrganizationUserUserDetails> existingUsers,
            List<ImportedOrganizationUser> newUsers)
    {
        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        org.UseDirectory = true;
        org.Seats = newUsers.Count + existingUsers.Count + 1;
    }
}
