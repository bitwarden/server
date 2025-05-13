using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Tokens;
using Bit.Core.Tools.Enums;
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
            List<ImportedOrganizationUser> newUsers
            )
    {
        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

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

        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);

        organizationUserRepository.GetManyDetailsByOrganizationAsync(org.Id)
            .Returns(existingUsers);
        organizationUserRepository.GetCountByOrganizationIdAsync(org.Id)
            .Returns(existingUsers.Count);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(org.Id, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(org.Id).Returns(true);


        await sutProvider.Sut.ImportAsync(org.Id, null, newUsers, null, false, EventSystemUser.PublicApi);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => !users.Any()));
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default);

        // Create new users
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .CreateManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.Count() == expectedNewUsersCount));

        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>().Received(1)
            .SendInvitesAsync(
                Arg.Is<SendInvitesRequest>(
                    info => info.Users.Length == expectedNewUsersCount &&
                            info.Organization == org));

        // Send events
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventsAsync(Arg.Is<IEnumerable<(OrganizationUser, EventType, EventSystemUser, DateTime?)>>(events =>
            events.Count() == expectedNewUsersCount));
        await sutProvider.GetDependency<IReferenceEventService>().Received(1)
            .RaiseEventAsync(Arg.Is<ReferenceEvent>(referenceEvent =>
            referenceEvent.Type == ReferenceEventType.InvitedUsers && referenceEvent.Id == org.Id &&
            referenceEvent.Users == expectedNewUsersCount));
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task OrgImportCreateNewUsersAndMarryExistingUser(SutProvider<ImportOrganizationUserCommand> sutProvider, Organization org, List<OrganizationUserUserDetails> existingUsers,
        List<ImportedOrganizationUser> newUsers)
    {
        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

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
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id)
            .Returns(existingUsers);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetCountByOrganizationIdAsync(org.Id)
            .Returns(existingUsers.Count);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(reInvitedUser.Id)
            .Returns(new OrganizationUser { Id = reInvitedUser.Id });

        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);

        var currentContext = sutProvider.GetDependency<ICurrentContext>();
        currentContext.ManageUsers(org.Id).Returns(true);

        await sutProvider.Sut.ImportAsync(org.Id, null, newUsers, null, false, EventSystemUser.PublicApi);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default, default);

        // Upserted existing user
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.Count() == 1));

        // Created and invited new users
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .CreateManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.Count() == expectedNewUsersCount));

        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>().Received(1)
            .SendInvitesAsync(Arg.Is<SendInvitesRequest>(request =>
                request.Users.Length == expectedNewUsersCount &&
                request.Organization == org));

        // Sent events
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventsAsync(Arg.Is<IEnumerable<(OrganizationUser, EventType, EventSystemUser, DateTime?)>>(events =>
            events.Count(e => e.Item2 == EventType.OrganizationUser_Invited) == expectedNewUsersCount));
        await sutProvider.GetDependency<IReferenceEventService>().Received(1)
            .RaiseEventAsync(Arg.Is<ReferenceEvent>(referenceEvent =>
            referenceEvent.Type == ReferenceEventType.InvitedUsers && referenceEvent.Id == org.Id &&
            referenceEvent.Users == expectedNewUsersCount));
    }

    private void SetupOrgUserRepositoryCreateManyAsyncMock(IOrganizationUserRepository organizationUserRepository)
    {
        organizationUserRepository.CreateManyAsync(Arg.Any<IEnumerable<OrganizationUser>>()).Returns(
            info =>
            {
                var orgUsers = info.Arg<IEnumerable<OrganizationUser>>();
                foreach (var orgUser in orgUsers)
                {
                    orgUser.Id = Guid.NewGuid();
                }

                return Task.FromResult<ICollection<Guid>>(orgUsers.Select(u => u.Id).ToList());
            }
        );

        organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>(), Arg.Any<IEnumerable<CollectionAccessSelection>>()).Returns(
            info =>
            {
                var orgUser = info.Arg<OrganizationUser>();
                orgUser.Id = Guid.NewGuid();
                return Task.FromResult<Guid>(orgUser.Id);
            }
        );
    }
}
