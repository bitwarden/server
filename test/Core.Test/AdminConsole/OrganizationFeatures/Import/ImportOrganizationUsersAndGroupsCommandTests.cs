using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.Import;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.StagedUsers;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
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

        sutProvider.GetDependency<IStripePaymentService>().HasSecretsManagerStandalone(org).Returns(true);
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

        await sutProvider.Sut.ImportAsync(org.Id, newGroups, importedUsers, new List<string>(), false, true);

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
    public async Task OverwriteExistingUsers_WhenRemovingUserWithoutMasterPassword_Throws(
            SutProvider<ImportOrganizationUsersAndGroupsCommand> sutProvider,
            Organization org, List<OrganizationUserUserDetails> existingUsers)
    {
        SetupOrganizationConfigForImport(sutProvider, org, existingUsers, []);

        // The removed user must be a non-owner without a master password. Owners are never included in
        // the removal set, so the user's type has to be pinned (AutoFixture may otherwise generate an owner).
        existingUsers.First().Type = OrganizationUserType.User;
        existingUsers.First().HasMasterPassword = false;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id).Returns(existingUsers);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.ImportAsync(org.Id, [], [], [], true, true));

        Assert.Contains("Sync failed. To proceed, disable the 'Remove and re-add users during next sync' setting and try again.", exception.Message);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .UpsertManyAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<IOrganizationService>().DidNotReceiveWithAnyArgs()
            .InviteUsersAsync(default, default, default, default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
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

        await sutProvider.Sut.ImportAsync(org.Id, newGroups, importedUsers, new List<string>(), false, true);

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

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task ImportAsync_InviteUsersAfterProvisioningDisabled_StagesNewUsers(
            SutProvider<ImportOrganizationUsersAndGroupsCommand> sutProvider,
            Organization org,
            List<OrganizationUserUserDetails> existingUsers,
            List<ImportedOrganizationUser> importedUsers,
            List<ImportedGroup> newGroups)
    {
        SetupOrganizationConfigForImport(sutProvider, org, existingUsers, importedUsers);

        var stagedUsers = new List<OrganizationUser>();

        // fix mocked email format, mock the staged OrganizationUsers the command returns.
        foreach (var u in importedUsers)
        {
            u.Email += "@bitwardentest.com";
            stagedUsers.Add(new OrganizationUser
            {
                Id = Guid.NewGuid(),
                Email = u.Email,
                ExternalId = u.ExternalId,
                Status = OrganizationUserStatusType.Staged
            });
        }

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id).Returns(existingUsers);

        CommandResult<ICollection<OrganizationUser>> commandResult = stagedUsers;
        sutProvider.GetDependency<ICreateStagedOrganizationUsersCommand>()
            .RunAsync(Arg.Any<CreateStagedOrganizationUsersRequest>())
            .Returns(commandResult);

        await sutProvider.Sut.ImportAsync(org.Id, newGroups, importedUsers, new List<string>(), false, false);

        // Stage new users instead of inviting them
        await sutProvider.GetDependency<ICreateStagedOrganizationUsersCommand>().Received(1)
            .RunAsync(Arg.Is<CreateStagedOrganizationUsersRequest>(r =>
                r.Organization == org &&
                r.EventSystemUser == EventSystemUser.PublicApi &&
                r.Users.Count() == importedUsers.Count &&
                r.Users.All(u => importedUsers.Any(iu => iu.Email == u.Email && iu.ExternalId == u.ExternalId))));

        // No invites are sent
        await sutProvider.GetDependency<IOrganizationService>().DidNotReceiveWithAnyArgs()
            .InviteUsersAsync(default, default, default, default);
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task ImportAsync_InviteUsersAfterProvisioningEnabled_InvitesNewUsers(
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

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id).Returns(existingUsers);
        sutProvider.GetDependency<IStripePaymentService>().HasSecretsManagerStandalone(org).Returns(true);
        sutProvider.GetDependency<IOrganizationService>().InviteUsersAsync(org.Id, Guid.Empty, EventSystemUser.PublicApi,
                Arg.Any<IEnumerable<(OrganizationUserInvite, string)>>())
            .Returns(orgUsers);

        await sutProvider.Sut.ImportAsync(org.Id, newGroups, importedUsers, new List<string>(), false, true);

        // Invitations are requested, so the existing invite flow runs unchanged
        await sutProvider.GetDependency<IOrganizationService>().Received(1)
            .InviteUsersAsync(org.Id, Guid.Empty, EventSystemUser.PublicApi,
                Arg.Is<IEnumerable<(OrganizationUserInvite, string)>>(invites => invites.Count() == importedUsers.Count));
        await sutProvider.GetDependency<ICreateStagedOrganizationUsersCommand>().DidNotReceiveWithAnyArgs()
            .RunAsync(default);
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task ImportAsync_WhenScopedApiKeyRemovesByExternalId_OnlyRemovesUsers(
            SutProvider<ImportOrganizationUsersAndGroupsCommand> sutProvider,
            Organization org)
    {
        var existingUsers = ExistingExternalMemberOfEachRole();
        SetupScopedImport(sutProvider, org, existingUsers, isScopedApiKey: true);

        await sutProvider.Sut.ImportAsync(org.Id, [], [], existingUsers.Select(u => u.ExternalId), false, false);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .DeleteManyAsync(Arg.Is<IEnumerable<Guid>>(ids =>
                ids.SequenceEqual(new[] { MemberOfRole(existingUsers, OrganizationUserType.User).Id })));
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task ImportAsync_WhenScopedApiKeyOverwritesExisting_OnlyRemovesUsers(
            SutProvider<ImportOrganizationUsersAndGroupsCommand> sutProvider,
            Organization org)
    {
        var existingUsers = ExistingExternalMemberOfEachRole();
        SetupScopedImport(sutProvider, org, existingUsers, isScopedApiKey: true);

        await sutProvider.Sut.ImportAsync(org.Id, [], [], [], true, false);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .DeleteManyAsync(Arg.Is<IEnumerable<Guid>>(ids =>
                ids.SequenceEqual(new[] { MemberOfRole(existingUsers, OrganizationUserType.User).Id })));
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task ImportAsync_WhenScopedApiKeyMatchesMembersByEmail_OnlyLinksUsers(
            SutProvider<ImportOrganizationUsersAndGroupsCommand> sutProvider,
            Organization org)
    {
        var existingUsers = ExistingExternalMemberOfEachRole();
        existingUsers.ForEach(u => u.ExternalId = null);
        var importedUsers = existingUsers
            .Select(u => new ImportedOrganizationUser { Email = u.Email, ExternalId = $"new-{u.Id}" })
            .ToList();
        SetupScopedImport(sutProvider, org, existingUsers, isScopedApiKey: true);
        var user = MemberOfRole(existingUsers, OrganizationUserType.User);
        var userEntity = new OrganizationUser { Id = user.Id, Email = user.Email };
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(info => info.Arg<IEnumerable<Guid>>().Contains(user.Id) ? [userEntity] : []);

        await sutProvider.Sut.ImportAsync(org.Id, [], importedUsers, [], false, false);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { user.Id })));
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users =>
                users.Single() == userEntity && userEntity.ExternalId == $"new-{user.Id}"));
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task ImportAsync_WhenLegacyKeyRemovesByExternalId_RemovesEveryoneButOwners(
            SutProvider<ImportOrganizationUsersAndGroupsCommand> sutProvider,
            Organization org)
    {
        var existingUsers = ExistingExternalMemberOfEachRole();
        SetupScopedImport(sutProvider, org, existingUsers, isScopedApiKey: false);
        var expectedIds = existingUsers.Where(u => u.Type != OrganizationUserType.Owner).Select(u => u.Id).ToHashSet();

        await sutProvider.Sut.ImportAsync(org.Id, [], [], existingUsers.Select(u => u.ExternalId), false, false);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .DeleteManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.ToHashSet().SetEquals(expectedIds)));
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task ImportAsync_WhenScopedApiKeySyncsExistingGroup_KeepsElevatedMembersInGroup(
            SutProvider<ImportOrganizationUsersAndGroupsCommand> sutProvider,
            Organization org)
    {
        var existingUsers = ExistingExternalMemberOfEachRole();
        SetupScopedImport(sutProvider, org, existingUsers, isScopedApiKey: true);
        var admin = MemberOfRole(existingUsers, OrganizationUserType.Admin);
        var user = MemberOfRole(existingUsers, OrganizationUserType.User);
        var group = SetupExistingGroup(sutProvider, org, admin.Id);

        await sutProvider.Sut.ImportAsync(org.Id, [ImportedGroupWithMembers(group, user)], [], [], false, false);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).UpdateUsersAsync(group.Id,
            Arg.Is<IEnumerable<Guid>>(ids => ids.ToHashSet().SetEquals(new[] { user.Id, admin.Id })),
            Arg.Any<DateTime>());
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task ImportAsync_WhenScopedApiKeySyncsNewGroup_OnlyAddsUsers(
            SutProvider<ImportOrganizationUsersAndGroupsCommand> sutProvider,
            Organization org, Group group)
    {
        var existingUsers = ExistingExternalMemberOfEachRole();
        SetupScopedImport(sutProvider, org, existingUsers, isScopedApiKey: true);
        org.UseGroups = true;
        group.OrganizationId = org.Id;
        sutProvider.GetDependency<IGroupRepository>().GetManyByOrganizationIdAsync(org.Id).Returns([]);
        var user = MemberOfRole(existingUsers, OrganizationUserType.User);

        await sutProvider.Sut.ImportAsync(org.Id, [ImportedGroupWithMembers(group, existingUsers.ToArray())], [], [],
            false, false);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).UpdateUsersAsync(group.Id,
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { user.Id })), Arg.Any<DateTime>());
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task ImportAsync_WhenLegacyKeySyncsExistingGroup_AddsAndRemovesEveryRole(
            SutProvider<ImportOrganizationUsersAndGroupsCommand> sutProvider,
            Organization org)
    {
        var existingUsers = ExistingExternalMemberOfEachRole();
        SetupScopedImport(sutProvider, org, existingUsers, isScopedApiKey: false);
        var admin = MemberOfRole(existingUsers, OrganizationUserType.Admin);
        var group = SetupExistingGroup(sutProvider, org, admin.Id);
        var importedMembers = existingUsers.Where(u => u.Type != OrganizationUserType.Admin).ToArray();

        await sutProvider.Sut.ImportAsync(org.Id, [ImportedGroupWithMembers(group, importedMembers)], [], [], false,
            false);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).UpdateUsersAsync(group.Id,
            Arg.Is<IEnumerable<Guid>>(ids => ids.ToHashSet().SetEquals(importedMembers.Select(u => u.Id))),
            Arg.Any<DateTime>());
    }

    private static Group SetupExistingGroup(
            SutProvider<ImportOrganizationUsersAndGroupsCommand> sutProvider,
            Organization org,
            Guid currentMemberId)
    {
        org.UseGroups = true;
        var group = new Group { Id = Guid.NewGuid(), OrganizationId = org.Id, Name = "Group", ExternalId = "external-group" };
        sutProvider.GetDependency<IGroupRepository>().GetManyByOrganizationIdAsync(org.Id).Returns([group]);
        sutProvider.GetDependency<IGroupRepository>().GetManyGroupUsersByOrganizationIdAsync(org.Id)
            .Returns([new GroupUser { GroupId = group.Id, OrganizationUserId = currentMemberId }]);
        return group;
    }

    private static ImportedGroup ImportedGroupWithMembers(Group group, params OrganizationUserUserDetails[] members) =>
        new()
        {
            Group = group,
            ExternalUserIds = members.Select(m => m.ExternalId).ToHashSet()
        };

    private static List<OrganizationUserUserDetails> ExistingExternalMemberOfEachRole() =>
        new[] { OrganizationUserType.Owner, OrganizationUserType.Admin, OrganizationUserType.Custom, OrganizationUserType.User }
            .Select(type => new OrganizationUserUserDetails
            {
                Id = Guid.NewGuid(),
                Email = $"{type}@bitwardentest.com".ToLowerInvariant(),
                ExternalId = $"external-{type}",
                Type = type,
                HasMasterPassword = true,
            })
            .ToList();

    private static OrganizationUserUserDetails MemberOfRole(
        IEnumerable<OrganizationUserUserDetails> members, OrganizationUserType type) =>
        members.Single(u => u.Type == type);

    private void SetupScopedImport(
            SutProvider<ImportOrganizationUsersAndGroupsCommand> sutProvider,
            Organization org,
            List<OrganizationUserUserDetails> existingUsers,
            bool isScopedApiKey)
    {
        SetupOrganizationConfigForImport(sutProvider, org, existingUsers, []);
        sutProvider.GetDependency<ICurrentContext>().IsScopedOrganizationApiKey.Returns(isScopedApiKey);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id)
            .Returns(existingUsers);
        CommandResult<ICollection<OrganizationUser>> noStagedUsers = new List<OrganizationUser>();
        sutProvider.GetDependency<ICreateStagedOrganizationUsersCommand>()
            .RunAsync(Arg.Any<CreateStagedOrganizationUsersRequest>())
            .Returns(noStagedUsers);
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
