using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class PushAutoConfirmNotificationCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task PushAsync_SendsNotificationToAdminsAndOwners(
        SutProvider<PushAutoConfirmNotificationCommand> sutProvider,
        Guid userId,
        Guid organizationId,
        OrganizationUser orgUser,
        List<OrganizationUserUserDetails> admins)
    {
        foreach (var admin in admins)
        {
            admin.UserId = Guid.NewGuid();
        }

        orgUser.Id = Guid.NewGuid();

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, userId)
            .Returns(orgUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organizationId, OrganizationUserType.Admin)
            .Returns(admins);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByRoleAsync(organizationId, OrganizationUserType.Custom)
            .Returns(new List<OrganizationUserUserDetails>());

        await sutProvider.Sut.PushAsync(userId, organizationId);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(admins.Count)
            .PushAsync(Arg.Is<PushNotification<AutoConfirmPushNotification>>(pn =>
                pn.Type == PushType.AutoConfirm &&
                pn.Target == NotificationTarget.User &&
                pn.Payload.OrganizationId == organizationId &&
                pn.Payload.TargetUserId == orgUser.Id &&
                pn.ExcludeCurrentContext == false));
    }

    [Theory]
    [BitAutoData]
    public async Task PushAsync_SendsNotificationToCustomUsersWithManageUsersPermission(
        SutProvider<PushAutoConfirmNotificationCommand> sutProvider,
        Guid userId,
        Guid organizationId,
        OrganizationUser orgUser,
        List<OrganizationUserUserDetails> customUsers)
    {
        foreach (var customUser in customUsers)
        {
            customUser.UserId = Guid.NewGuid();
            customUser.Permissions = "{\"manageUsers\":true}";
        }

        orgUser.Id = Guid.NewGuid();

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, userId)
            .Returns(orgUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organizationId, OrganizationUserType.Admin)
            .Returns(new List<OrganizationUserUserDetails>());

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByRoleAsync(organizationId, OrganizationUserType.Custom)
            .Returns(customUsers);

        await sutProvider.Sut.PushAsync(userId, organizationId);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(customUsers.Count)
            .PushAsync(Arg.Is<PushNotification<AutoConfirmPushNotification>>(pn =>
                pn.Type == PushType.AutoConfirm &&
                pn.Target == NotificationTarget.User &&
                pn.Payload.OrganizationId == organizationId &&
                pn.Payload.TargetUserId == orgUser.Id &&
                pn.ExcludeCurrentContext == false));
    }

    [Theory]
    [BitAutoData]
    public async Task PushAsync_DoesNotSendToCustomUsersWithoutManageUsersPermission(
        SutProvider<PushAutoConfirmNotificationCommand> sutProvider,
        Guid userId,
        Guid organizationId,
        OrganizationUser orgUser,
        List<OrganizationUserUserDetails> customUsers)
    {
        foreach (var customUser in customUsers)
        {
            customUser.UserId = Guid.NewGuid();
            customUser.Permissions = "{\"manageUsers\":false}";
        }

        orgUser.Id = Guid.NewGuid();

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, userId)
            .Returns(orgUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organizationId, OrganizationUserType.Admin)
            .Returns(new List<OrganizationUserUserDetails>());

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByRoleAsync(organizationId, OrganizationUserType.Custom)
            .Returns(customUsers);

        await sutProvider.Sut.PushAsync(userId, organizationId);

        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushAsync(Arg.Any<PushNotification<AutoConfirmPushNotification>>());
    }

    [Theory]
    [BitAutoData]
    public async Task PushAsync_SendsToAdminsAndCustomUsersWithManageUsers(
        SutProvider<PushAutoConfirmNotificationCommand> sutProvider,
        Guid userId,
        Guid organizationId,
        OrganizationUser orgUser,
        List<OrganizationUserUserDetails> admins,
        List<OrganizationUserUserDetails> customUsersWithPermission,
        List<OrganizationUserUserDetails> customUsersWithoutPermission)
    {
        foreach (var admin in admins)
        {
            admin.UserId = Guid.NewGuid();
        }

        foreach (var customUser in customUsersWithPermission)
        {
            customUser.UserId = Guid.NewGuid();
            customUser.Permissions = "{\"manageUsers\":true}";
        }

        foreach (var customUser in customUsersWithoutPermission)
        {
            customUser.UserId = Guid.NewGuid();
            customUser.Permissions = "{\"manageUsers\":false}";
        }

        orgUser.Id = Guid.NewGuid();

        var allCustomUsers = customUsersWithPermission.Concat(customUsersWithoutPermission).ToList();

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, userId)
            .Returns(orgUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organizationId, OrganizationUserType.Admin)
            .Returns(admins);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByRoleAsync(organizationId, OrganizationUserType.Custom)
            .Returns(allCustomUsers);

        await sutProvider.Sut.PushAsync(userId, organizationId);

        var expectedNotificationCount = admins.Count + customUsersWithPermission.Count;
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(expectedNotificationCount)
            .PushAsync(Arg.Is<PushNotification<AutoConfirmPushNotification>>(pn =>
                pn.Type == PushType.AutoConfirm &&
                pn.Target == NotificationTarget.User &&
                pn.Payload.OrganizationId == organizationId &&
                pn.Payload.TargetUserId == orgUser.Id &&
                pn.ExcludeCurrentContext == false));
    }

    [Theory]
    [BitAutoData]
    public async Task PushAsync_SkipsUsersWithoutUserId(
        SutProvider<PushAutoConfirmNotificationCommand> sutProvider,
        Guid userId,
        Guid organizationId,
        OrganizationUser orgUser,
        List<OrganizationUserUserDetails> admins)
    {
        admins[0].UserId = Guid.NewGuid();
        admins[1].UserId = null;
        admins[2].UserId = Guid.NewGuid();

        orgUser.Id = Guid.NewGuid();

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, userId)
            .Returns(orgUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organizationId, OrganizationUserType.Admin)
            .Returns(admins);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByRoleAsync(organizationId, OrganizationUserType.Custom)
            .Returns(new List<OrganizationUserUserDetails>());

        await sutProvider.Sut.PushAsync(userId, organizationId);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(2)
            .PushAsync(Arg.Is<PushNotification<AutoConfirmPushNotification>>(pn =>
                pn.Type == PushType.AutoConfirm));
    }

    [Theory]
    [BitAutoData]
    public async Task PushAsync_DeduplicatesUserIds(
        SutProvider<PushAutoConfirmNotificationCommand> sutProvider,
        Guid userId,
        Guid organizationId,
        OrganizationUser orgUser,
        Guid duplicateUserId)
    {
        var admin1 = new OrganizationUserUserDetails { UserId = duplicateUserId };
        var admin2 = new OrganizationUserUserDetails { UserId = duplicateUserId };
        var customUser = new OrganizationUserUserDetails
        {
            UserId = duplicateUserId,
            Permissions = "{\"manageUsers\":true}"
        };

        orgUser.Id = Guid.NewGuid();

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, userId)
            .Returns(orgUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organizationId, OrganizationUserType.Admin)
            .Returns(new List<OrganizationUserUserDetails> { admin1, admin2 });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByRoleAsync(organizationId, OrganizationUserType.Custom)
            .Returns(new List<OrganizationUserUserDetails> { customUser });

        await sutProvider.Sut.PushAsync(userId, organizationId);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<AutoConfirmPushNotification>>(pn =>
                pn.TargetId == duplicateUserId));
    }

    [Theory]
    [BitAutoData]
    public async Task PushAsync_OrganizationUserNotFound_ThrowsException(
        SutProvider<PushAutoConfirmNotificationCommand> sutProvider,
        Guid userId,
        Guid organizationId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, userId)
            .Returns((OrganizationUser)null);

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            sutProvider.Sut.PushAsync(userId, organizationId));

        Assert.Equal("Organization user not found", exception.Message);

        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushAsync(Arg.Any<PushNotification<AutoConfirmPushNotification>>());
    }
}
