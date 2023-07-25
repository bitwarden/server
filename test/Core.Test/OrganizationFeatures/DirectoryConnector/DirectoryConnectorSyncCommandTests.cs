using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.OrganizationFeatures.DirectoryConnector;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.DirectoryConnector;

[SutProviderCustomize]
public class DirectoryConnectorSyncCommandTests
{
    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task OrgImportCreateNewUsers(SutProvider<DirectoryConnectorSyncCommand> sutProvider, Guid userId,
        Organization org, List<OrganizationUserUserDetails> existingUsers, List<ImportedOrganizationUser> newUsers)
    {
        org.UseDirectory = true;
        org.Seats = 10;
        newUsers.Add(new ImportedOrganizationUser
        {
            Email = existingUsers.First().Email,
            ExternalId = existingUsers.First().ExternalId
        });

        existingUsers.First().Type = OrganizationUserType.Owner;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id)
            .Returns(existingUsers);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetCountByOrganizationIdAsync(org.Id)
            .Returns(existingUsers.Count);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByOrganizationAsync(org.Id, OrganizationUserType.Owner)
            .Returns(existingUsers.Select(u => new OrganizationUser { Status = OrganizationUserStatusType.Confirmed, Type = OrganizationUserType.Owner, Id = u.Id }).ToList());
        sutProvider.GetDependency<IInviteOrganizationUserCommand>()
            .InviteUsersAsync(org.Id, userId,
                Arg.Any<IEnumerable<(OrganizationUserInvite invite, string externalId)>>())
            .Returns(new List<OrganizationUser>());

        await sutProvider.Sut.SyncOrganizationAsync(org.Id, userId, null, newUsers, null, false);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.Count() == 0));
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<IInviteOrganizationUserCommand>().Received(1)
            .InviteUsersAsync(org.Id, userId, Arg.Is<IEnumerable<(OrganizationUserInvite invite, string externalId)>>(i => i.All(invite => newUsers.Any(n => invite.invite.Emails.Contains(n.Email)))));
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task OrgImportCreateNewUsersAndMarryExistingUser(SutProvider<DirectoryConnectorSyncCommand> sutProvider,
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

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id)
            .Returns(existingUsers);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetCountByOrganizationIdAsync(org.Id)
            .Returns(existingUsers.Count);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(reInvitedUser.Id)
            .Returns(new OrganizationUser { Id = reInvitedUser.Id });
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByOrganizationAsync(org.Id, OrganizationUserType.Owner)
            .Returns(existingUsers.Select(u => new OrganizationUser { Status = OrganizationUserStatusType.Confirmed, Type = OrganizationUserType.Owner, Id = u.Id }).ToList());
        sutProvider.GetDependency<IInviteOrganizationUserCommand>()
            .InviteUsersAsync(org.Id, userId,
                Arg.Any<IEnumerable<(OrganizationUserInvite invite, string externalId)>>())
            .Returns(new List<OrganizationUser>());

        await sutProvider.Sut.SyncOrganizationAsync(org.Id, userId, null, newUsers, null, false);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default, default);

        // Upserted existing user
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.Count() == 1));

        // Invited new users
        await sutProvider.GetDependency<IInviteOrganizationUserCommand>().Received(1)
            .InviteUsersAsync(org.Id, userId, Arg.Is<IEnumerable<(OrganizationUserInvite invite, string externalId)>>(i => i.All(invite => newUsers.Any(n => invite.invite.Emails.Contains(n.Email)))));
    }
}