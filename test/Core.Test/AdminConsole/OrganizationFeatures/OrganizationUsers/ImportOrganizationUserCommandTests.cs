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
    public async Task OrgImportCallsInviteOrgUserCommand(
            SutProvider<ImportOrganizationUserCommand> sutProvider,
            Organization org,
            List<OrganizationUserUserDetails> existingUsers,
            List<ImportedOrganizationUser> newUsers,
            List<ImportedGroup> newGroups)
    {
        SetupOrganizationConfigForImport(sutProvider, org, existingUsers, newUsers);

        newUsers.Add(new ImportedOrganizationUser
        {
            Email = existingUsers.First().Email,
            ExternalId = existingUsers.First().ExternalId
        });

        foreach (var u in newUsers)
        {
            u.Email += "@bitwardentest.com";
        }

        existingUsers.First().Type = OrganizationUserType.Owner;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IPaymentService>().HasSecretsManagerStandalone(org).Returns(false);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id).Returns(existingUsers);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetCountByOrganizationIdAsync(org.Id).Returns(existingUsers.Count);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(org.Id).Returns(true);
        sutProvider.GetDependency<IInviteOrganizationUsersCommand>().InviteImportedOrganizationUsersAsync(Arg.Any<InviteOrganizationUsersRequest>())
            .Returns(new Success<InviteOrganizationUsersResponse>(new InviteOrganizationUsersResponse(org.Id)));

        await sutProvider.Sut.ImportAsync(org.Id, newGroups, newUsers, new List<string>(), false);

        await sutProvider.GetDependency<IInviteOrganizationUsersCommand>().Received(1)
            .InviteImportedOrganizationUsersAsync(Arg.Any<InviteOrganizationUsersRequest>());
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => !users.Any()));
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default);

        // Send events
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUserUserDetails, EventType, EventSystemUser, DateTime?)>>());
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
        foreach (var u in existingUsers)
        {
            u.Email += "@bitwardentest.com";
        }
        foreach (var u in newUsers)
        {
            u.Email += "@bitwardentest.com";
        }

        newUsers.Add(new ImportedOrganizationUser
        {
            Email = reInvitedUser.Email,
            ExternalId = reInvitedUser.Email,
        });

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id).Returns(existingUsers);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetCountByOrganizationIdAsync(org.Id).Returns(existingUsers.Count);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(Arg.Any<IEnumerable<Guid>>()).Returns(new List<OrganizationUser> { new OrganizationUser { Id = reInvitedUser.Id } });
        sutProvider.GetDependency<IInviteOrganizationUsersCommand>().InviteImportedOrganizationUsersAsync(Arg.Any<InviteOrganizationUsersRequest>())
            .Returns(new Success<InviteOrganizationUsersResponse>(new InviteOrganizationUsersResponse(org.Id)));

        await sutProvider.Sut.ImportAsync(org.Id, newGroups, newUsers, new List<string>(), false);

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
            .InviteImportedOrganizationUsersAsync(Arg.Any<InviteOrganizationUsersRequest>());

        // Send events
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUserUserDetails, EventType, EventSystemUser, DateTime?)>>());

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
