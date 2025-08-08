using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.Import;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
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

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Import;

public class ImportOrganizationUsersAndGroupsCommandTests
{

    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory = new FakeDataProtectorTokenFactory<OrgUserInviteTokenable>();

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task OrgImportCallsInviteOrgUserCommand(
            SutProvider<ImportOrganizationUsersAndGroupsCommand> sutProvider,
            Organization org,
            List<OrganizationUserUserDetails> existingUsers,
            List<ImportedOrganizationUser> importedUsers,
            List<ImportedGroup> newGroups)
    {
        SetupOrganizationConfigForImport(sutProvider, org, existingUsers, importedUsers);

        var orgUsers = new List<OrganizationUser>();

        // fix mocked email format, mock OrganizationUsers.
        foreach (var u in importedUsers)
        {
            u.Email += "@bitwardentest.com";
            orgUsers.Add(new OrganizationUser { Email = u.Email, ExternalId = u.ExternalId });
        }

        importedUsers.Add(new ImportedOrganizationUser
        {
            Email = existingUsers.First().Email,
            ExternalId = existingUsers.First().ExternalId
        });


        existingUsers.First().Type = OrganizationUserType.Owner;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);

        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);

        sutProvider.GetDependency<IPaymentService>().HasSecretsManagerStandalone(org).Returns(true);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id).Returns(existingUsers);
        sutProvider.GetDependency<IOrganizationRepository>().GetOccupiedSeatCountByOrganizationIdAsync(org.Id).Returns(
            new OrganizationSeatCounts
            {
                Users = existingUsers.Count,
                Sponsored = 0
            });
        sutProvider.GetDependency<IOrganizationService>().InviteUsersAsync(org.Id, Guid.Empty, EventSystemUser.PublicApi,
                Arg.Any<IEnumerable<(OrganizationUserInvite, string)>>())
            .Returns(orgUsers);

        await sutProvider.Sut.ImportAsync(org.Id, newGroups, importedUsers, new List<string>(), false);

        var expectedNewUsersCount = importedUsers.Count - 1;

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => !users.Any()));
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default);

        // Send Invites
        await sutProvider.GetDependency<IOrganizationService>().Received(1).
            InviteUsersAsync(org.Id, Guid.Empty, EventSystemUser.PublicApi,
                    Arg.Is<IEnumerable<(OrganizationUserInvite, string)>>(invites => invites.Count() == expectedNewUsersCount));

        // Send events
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUserUserDetails, EventType, EventSystemUser, DateTime?)>>());
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task OrgImportCreateNewUsersAndMarryExistingUser(
            SutProvider<ImportOrganizationUsersAndGroupsCommand> sutProvider,
            Organization org,
            List<OrganizationUserUserDetails> existingUsers,
            List<ImportedOrganizationUser> importedUsers,
            List<ImportedGroup> newGroups)
    {
        SetupOrganizationConfigForImport(sutProvider, org, existingUsers, importedUsers);

        var orgUsers = new List<OrganizationUser>();
        var reInvitedUser = existingUsers.First();
        // Existing user has no external ID. This will make the SUT call UpsertManyAsync
        reInvitedUser.ExternalId = "";

        // Mock an existing org user for this "existing" user
        var reInvitedOrgUser = new OrganizationUser { Email = reInvitedUser.Email, Id = reInvitedUser.Id };

        // fix email formatting, mock orgUsers to be returned
        foreach (var u in existingUsers)
        {
            u.Email += "@bitwardentest.com";
            orgUsers.Add(new OrganizationUser { Email = u.Email, ExternalId = u.ExternalId });
        }
        foreach (var u in importedUsers)
        {
            u.Email += "@bitwardentest.com";
            orgUsers.Add(new OrganizationUser { Email = u.Email, ExternalId = u.ExternalId });
        }

        // add the existing user to be re-imported
        importedUsers.Add(new ImportedOrganizationUser
        {
            Email = reInvitedUser.Email,
            ExternalId = reInvitedUser.Email,
        });

        var expectedNewUsersCount = importedUsers.Count - 1;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);

        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationUser>([reInvitedOrgUser]));
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id).Returns(existingUsers);
        sutProvider.GetDependency<IOrganizationRepository>().GetOccupiedSeatCountByOrganizationIdAsync(org.Id).Returns(
            new OrganizationSeatCounts
            {
                Users = existingUsers.Count,
                Sponsored = 0
            });

        sutProvider.GetDependency<IOrganizationService>().InviteUsersAsync(org.Id, Guid.Empty, EventSystemUser.PublicApi,
                Arg.Any<IEnumerable<(OrganizationUserInvite, string)>>())
            .Returns(orgUsers);

        await sutProvider.Sut.ImportAsync(org.Id, newGroups, importedUsers, new List<string>(), false);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default, default);

        // Upserted existing user
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.Count() == 1 && users.First() == reInvitedOrgUser));

        // Send Invites
        await sutProvider.GetDependency<IOrganizationService>().Received(1).
            InviteUsersAsync(org.Id, Guid.Empty, EventSystemUser.PublicApi,
                    Arg.Is<IEnumerable<(OrganizationUserInvite, string)>>(invites => invites.Count() == expectedNewUsersCount));

        // Send events
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUserUserDetails, EventType, EventSystemUser, DateTime?)>>());
    }

    private void SetupOrganizationConfigForImport(
            SutProvider<ImportOrganizationUsersAndGroupsCommand> sutProvider,
            Organization org,
            List<OrganizationUserUserDetails> existingUsers,
            List<ImportedOrganizationUser> importedUsers)
    {
        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        org.UseDirectory = true;
        org.Seats = importedUsers.Count + existingUsers.Count + 1;
    }

    // Must set real guids in order for dictionary of guids to not throw aggregate exceptions
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
