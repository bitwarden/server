using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

[SutProviderCustomize]
public class ResendOrganizationInviteCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task ResendInviteAsync_WhenValidUserAndOrganization_SendsInvite(
        Organization organization,
        OrganizationUser organizationUser,
        SutProvider<ResendOrganizationInviteCommand> sutProvider)
    {
        // Arrange
        organizationUser.OrganizationId = organization.Id;
        organizationUser.Status = OrganizationUserStatusType.Invited;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        // Act
        await sutProvider.Sut.ResendInviteAsync(organization.Id, invitingUserId: null, organizationUser.Id);

        // Assert
        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>()
            .Received(1)
            .SendInvitesAsync(Arg.Is<SendInvitesRequest>(req =>
                req.Organization == organization &&
                req.Users.Length == 1 &&
                req.Users[0] == organizationUser &&
                req.InitOrganization == false));
    }

    [Theory]
    [BitAutoData]
    public async Task ResendInviteAsync_WhenNoSystemUser_LogsReinvitedEventForActingUser(
        Organization organization,
        OrganizationUser organizationUser,
        Guid invitingUserId,
        SutProvider<ResendOrganizationInviteCommand> sutProvider)
    {
        // Arrange
        ArrangeValidInvite(sutProvider, organization, organizationUser);

        // Act
        await sutProvider.Sut.ResendInviteAsync(organization.Id, invitingUserId, organizationUser.Id);

        // Assert
        var eventService = sutProvider.GetDependency<IEventService>();
        await eventService.Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Reinvited);
        await eventService.DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(default(OrganizationUser)!, default, default(EventSystemUser));
    }

    [Theory]
    [BitAutoData]
    public async Task ResendInviteAsync_WhenSystemUserIsSet_LogsReinvitedEventForSystemUser(
        Organization organization,
        OrganizationUser organizationUser,
        SutProvider<ResendOrganizationInviteCommand> sutProvider)
    {
        // Arrange
        ArrangeValidInvite(sutProvider, organization, organizationUser);

        // Act
        await sutProvider.Sut.ResendInviteAsync(organization.Id, invitingUserId: null, organizationUser.Id,
            systemUser: EventSystemUser.PublicApi);

        // Assert
        var eventService = sutProvider.GetDependency<IEventService>();
        await eventService.Received(1).LogOrganizationUserEventAsync(organizationUser,
            EventType.OrganizationUser_Reinvited, EventSystemUser.PublicApi);
        await eventService.DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(default(OrganizationUser)!, default);
    }

    [Theory]
    [BitAutoData]
    public async Task ResendInviteAsync_WhenInitOrganizationTrue_SendsInviteWithInitFlag(
        Organization organization,
        OrganizationUser organizationUser,
        SutProvider<ResendOrganizationInviteCommand> sutProvider)
    {
        // Arrange
        organizationUser.OrganizationId = organization.Id;
        organizationUser.Status = OrganizationUserStatusType.Invited;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        // Act
        await sutProvider.Sut.ResendInviteAsync(organization.Id, invitingUserId: null, organizationUser.Id, initOrganization: true);

        // Assert
        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>()
            .Received(1)
            .SendInvitesAsync(Arg.Is<SendInvitesRequest>(req =>
                req.Organization == organization &&
                req.Users.Length == 1 &&
                req.Users[0] == organizationUser &&
                req.InitOrganization == true));
    }

    [Theory]
    [BitAutoData]
    public async Task ResendInviteAsync_WhenOrganizationUserInvalid_ThrowsBadRequest(
        Organization organization,
        OrganizationUser organizationUser,
        SutProvider<ResendOrganizationInviteCommand> sutProvider)
    {
        // Arrange
        organizationUser.OrganizationId = organization.Id;
        organizationUser.Status = OrganizationUserStatusType.Accepted;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        // Act + Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.ResendInviteAsync(organization.Id, invitingUserId: null, organizationUser.Id));

        Assert.Equal("User invalid.", ex.Message);

        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>()
            .DidNotReceive()
            .SendInvitesAsync(Arg.Any<SendInvitesRequest>());
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(default(OrganizationUser)!, default);
    }

    [Theory]
    [BitAutoData]
    public async Task ResendInviteAsync_WhenOrganizationNotFound_ThrowsBadRequest(
        Organization organization,
        OrganizationUser organizationUser,
        SutProvider<ResendOrganizationInviteCommand> sutProvider)
    {
        // Arrange
        organizationUser.OrganizationId = organization.Id;
        organizationUser.Status = OrganizationUserStatusType.Invited;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns((Organization?)null);

        // Act + Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.ResendInviteAsync(organization.Id, invitingUserId: null, organizationUser.Id));

        Assert.Equal("Organization invalid.", ex.Message);

        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>()
            .DidNotReceive()
            .SendInvitesAsync(Arg.Any<SendInvitesRequest>());
    }

    private static void ArrangeValidInvite(SutProvider<ResendOrganizationInviteCommand> sutProvider,
        Organization organization, OrganizationUser organizationUser)
    {
        organizationUser.OrganizationId = organization.Id;
        organizationUser.Status = OrganizationUserStatusType.Invited;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
    }
}
