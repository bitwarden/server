#nullable enable
using System.Security.Claims;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.NotificationCenter.Authorization;
using Bit.Core.NotificationCenter.Commands;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Test.NotificationCenter.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.NotificationCenter.Commands;

[SutProviderCustomize]
[NotificationCustomize]
[NotificationStatusCustomize]
public class MarkNotificationReadCommandTest
{
    private static void Setup(SutProvider<MarkNotificationReadCommand> sutProvider,
        Guid notificationId, Guid? userId, Notification? notification, NotificationStatus? notificationStatus,
        bool authorizedNotification = false, bool authorizedCreate = false, bool authorizedUpdate = false)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<INotificationRepository>()
            .GetByIdAsync(notificationId)
            .Returns(notification);
        sutProvider.GetDependency<INotificationStatusRepository>()
            .GetByNotificationIdAndUserIdAsync(notificationId, userId ?? Arg.Any<Guid>())
            .Returns(notificationStatus);
        sutProvider.GetDependency<INotificationStatusRepository>()
            .CreateAsync(Arg.Any<NotificationStatus>());
        sutProvider.GetDependency<INotificationStatusRepository>()
            .UpdateAsync(notificationStatus ?? Arg.Any<NotificationStatus>());
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), notification ?? Arg.Any<Notification>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(NotificationOperations.Read)))
            .Returns(authorizedNotification ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), notificationStatus ?? Arg.Any<NotificationStatus>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(NotificationStatusOperations.Create)))
            .Returns(authorizedCreate ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), notificationStatus ?? Arg.Any<NotificationStatus>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(NotificationStatusOperations.Update)))
            .Returns(authorizedUpdate ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        sutProvider.GetDependency<INotificationStatusRepository>().ClearReceivedCalls();
    }

    [Theory]
    [BitAutoData]
    public async Task MarkReadAsync_NotLoggedIn_NotFoundException(
        SutProvider<MarkNotificationReadCommand> sutProvider,
        Guid notificationId, Notification notification, NotificationStatus notificationStatus)
    {
        Setup(sutProvider, notificationId, userId: null, notification, notificationStatus, true, true, true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.MarkReadAsync(notificationId));
    }

    [Theory]
    [BitAutoData]
    public async Task MarkReadAsync_NotificationNotFound_NotFoundException(
        SutProvider<MarkNotificationReadCommand> sutProvider,
        Guid notificationId, Guid userId, NotificationStatus notificationStatus)
    {
        Setup(sutProvider, notificationId, userId, notification: null, notificationStatus, true, true, true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.MarkReadAsync(notificationId));
    }

    [Theory]
    [BitAutoData]
    public async Task MarkReadAsync_ReadRequirementNotificationNotAuthorized_NotFoundException(
        SutProvider<MarkNotificationReadCommand> sutProvider,
        Guid notificationId, Guid userId, Notification notification, NotificationStatus notificationStatus)
    {
        Setup(sutProvider, notificationId, userId, notification, notificationStatus, authorizedNotification: false,
            true, true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.MarkReadAsync(notificationId));
    }

    [Theory]
    [BitAutoData]
    public async Task MarkReadAsync_CreateRequirementNotAuthorized_NotFoundException(
        SutProvider<MarkNotificationReadCommand> sutProvider,
        Guid notificationId, Guid userId, Notification notification)
    {
        Setup(sutProvider, notificationId, userId, notification, notificationStatus: null, true,
            authorizedCreate: false, true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.MarkReadAsync(notificationId));
    }

    [Theory]
    [BitAutoData]
    public async Task MarkReadAsync_UpdateRequirementNotAuthorized_NotFoundException(
        SutProvider<MarkNotificationReadCommand> sutProvider,
        Guid notificationId, Guid userId, Notification notification, NotificationStatus notificationStatus)
    {
        Setup(sutProvider, notificationId, userId, notification, notificationStatus, true, true,
            authorizedUpdate: false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.MarkReadAsync(notificationId));
    }

    [Theory]
    [BitAutoData]
    public async Task MarkReadAsync_NotificationStatusNotFoundCreateAuthorized_NotificationStatusCreated(
        SutProvider<MarkNotificationReadCommand> sutProvider,
        Guid notificationId, Guid userId, Notification notification)
    {
        Setup(sutProvider, notificationId, userId, notification, notificationStatus: null, true, true, true);

        await sutProvider.Sut.MarkReadAsync(notificationId);

        await sutProvider.GetDependency<INotificationStatusRepository>().Received(1)
            .CreateAsync(Arg.Is<NotificationStatus>(ns =>
                ns.NotificationId == notificationId && ns.UserId == userId && !ns.DeletedDate.HasValue &&
                ns.ReadDate.HasValue && DateTime.UtcNow - ns.ReadDate.Value < TimeSpan.FromMinutes(1)));
    }

    [Theory]
    [BitAutoData]
    public async Task MarkReadAsync_NotificationStatusFoundCreateAuthorized_NotificationStatusUpdated(
        SutProvider<MarkNotificationReadCommand> sutProvider,
        Guid notificationId, Guid userId, Notification notification, NotificationStatus notificationStatus)
    {
        var readDate = notificationStatus.ReadDate;

        Setup(sutProvider, notificationId, userId, notification, notificationStatus, true, true, true);

        await sutProvider.Sut.MarkReadAsync(notificationId);

        await sutProvider.GetDependency<INotificationStatusRepository>().Received(1)
            .UpdateAsync(Arg.Is<NotificationStatus>(ns =>
                ns.Equals(notificationStatus) &&
                ns.NotificationId == notificationStatus.NotificationId && ns.UserId == notificationStatus.UserId &&
                ns.DeletedDate == notificationStatus.DeletedDate && ns.ReadDate != readDate &&
                ns.ReadDate.HasValue &&
                DateTime.UtcNow - ns.ReadDate.Value < TimeSpan.FromMinutes(1)));
    }
}
