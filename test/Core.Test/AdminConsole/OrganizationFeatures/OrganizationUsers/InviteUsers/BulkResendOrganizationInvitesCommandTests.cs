using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

[SutProviderCustomize]
public class BulkResendOrganizationInvitesCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task BulkResendInvitesAsync_ValidatesUsersAndSendsBatchInvite(
        Organization organization,
        OrganizationUser validUser1,
        OrganizationUser validUser2,
        OrganizationUser acceptedUser,
        OrganizationUser wrongOrgUser,
        SutProvider<BulkResendOrganizationInvitesCommand> sutProvider)
    {
        validUser1.OrganizationId = organization.Id;
        validUser1.Status = OrganizationUserStatusType.Invited;
        validUser2.OrganizationId = organization.Id;
        validUser2.Status = OrganizationUserStatusType.Invited;
        acceptedUser.OrganizationId = organization.Id;
        acceptedUser.Status = OrganizationUserStatusType.Accepted;
        wrongOrgUser.OrganizationId = Guid.NewGuid();
        wrongOrgUser.Status = OrganizationUserStatusType.Invited;

        var users = new List<OrganizationUser> { validUser1, validUser2, acceptedUser, wrongOrgUser };
        var userIds = users.Select(u => u.Id).ToList();

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(userIds).Returns(users);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var result = (await sutProvider.Sut.BulkResendInvitesAsync(organization.Id, null, userIds)).ToList();

        Assert.Equal(4, result.Count);
        Assert.Equal(2, result.Count(r => string.IsNullOrEmpty(r.Item2)));
        Assert.Equal(2, result.Count(r => r.Item2 == "User invalid."));

        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>()
            .Received(1)
            .SendInvitesAsync(Arg.Is<SendInvitesRequest>(req =>
                req.Organization == organization &&
                req.Users.Length == 2 &&
                req.InitOrganization == false));
    }

    [Theory]
    [BitAutoData]
    public async Task BulkResendInvitesAsync_AllInvalidUsers_DoesNotSendInvites(
        Organization organization,
        List<OrganizationUser> organizationUsers,
        SutProvider<BulkResendOrganizationInvitesCommand> sutProvider)
    {
        foreach (var user in organizationUsers)
        {
            user.OrganizationId = organization.Id;
            user.Status = OrganizationUserStatusType.Confirmed;
        }

        var userIds = organizationUsers.Select(u => u.Id).ToList();
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(userIds).Returns(organizationUsers);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var result = (await sutProvider.Sut.BulkResendInvitesAsync(organization.Id, null, userIds)).ToList();

        Assert.Equal(organizationUsers.Count, result.Count);
        Assert.All(result, r => Assert.Equal("User invalid.", r.Item2));
        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>().DidNotReceive()
            .SendInvitesAsync(Arg.Any<SendInvitesRequest>());
    }

    [Theory]
    [BitAutoData]
    public async Task BulkResendInvitesAsync_OrganizationNotFound_ThrowsNotFoundException(
        Guid organizationId,
        List<Guid> userIds,
        List<OrganizationUser> organizationUsers,
        SutProvider<BulkResendOrganizationInvitesCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(userIds).Returns(organizationUsers);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns((Organization?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.BulkResendInvitesAsync(organizationId, null, userIds));
    }

    [Theory]
    [BitAutoData]
    public async Task BulkResendInvitesAsync_EmptyUserList_ReturnsEmpty(
        Organization organization,
        SutProvider<BulkResendOrganizationInvitesCommand> sutProvider)
    {
        var emptyUserIds = new List<Guid>();
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(emptyUserIds).Returns(new List<OrganizationUser>());
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var result = await sutProvider.Sut.BulkResendInvitesAsync(organization.Id, null, emptyUserIds);

        Assert.Empty(result);
        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>().DidNotReceive()
            .SendInvitesAsync(Arg.Any<SendInvitesRequest>());
    }
}
