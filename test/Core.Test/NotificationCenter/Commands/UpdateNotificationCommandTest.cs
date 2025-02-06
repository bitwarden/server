#nullable enable
using System.Security.Claims;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.NotificationCenter.Authorization;
using Bit.Core.NotificationCenter.Commands;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Enums;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Test.NotificationCenter.AutoFixture;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.NotificationCenter.Commands;

[SutProviderCustomize]
[NotificationCustomize]
public class UpdateNotificationCommandTest
{
    private static void Setup(SutProvider<UpdateNotificationCommand> sutProvider,
        Guid notificationId, Notification? notification, bool authorized = false)
    {
        sutProvider.GetDependency<INotificationRepository>()
            .GetByIdAsync(notificationId)
            .Returns(notification);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), notification ?? Arg.Any<Notification>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(NotificationOperations.Update)))
            .Returns(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        sutProvider.GetDependency<INotificationRepository>().ClearReceivedCalls();
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_NotificationNotFound_NotFoundException(
        SutProvider<UpdateNotificationCommand> sutProvider,
        Notification notification)
    {
        Setup(sutProvider, notification.Id, notification: null, true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateAsync(notification));
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_AuthorizationFailed_NotFoundException(
        SutProvider<UpdateNotificationCommand> sutProvider,
        Notification notification)
    {
        Setup(sutProvider, notification.Id, notification, authorized: false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateAsync(notification));
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_Authorized_NotificationCreated(
        SutProvider<UpdateNotificationCommand> sutProvider,
        Notification notification)
    {
        notification.Priority = Priority.Medium;
        notification.ClientType = ClientType.Web;
        notification.Title = "Title";
        notification.Body = "Body";
        notification.RevisionDate = DateTime.UtcNow.AddMinutes(-60);

        Setup(sutProvider, notification.Id, notification, true);

        var notificationToUpdate = CoreHelpers.CloneObject(notification);
        notificationToUpdate.Priority = Priority.High;
        notificationToUpdate.ClientType = ClientType.Mobile;
        notificationToUpdate.Title = "Updated Title";
        notificationToUpdate.Body = "Updated Body";
        notificationToUpdate.RevisionDate = DateTime.UtcNow.AddMinutes(-30);

        await sutProvider.Sut.UpdateAsync(notificationToUpdate);

        await sutProvider.GetDependency<INotificationRepository>().Received(1)
            .ReplaceAsync(Arg.Is<Notification>(n =>
                // Not updated fields
                n.Id == notificationToUpdate.Id && n.Global == notificationToUpdate.Global &&
                n.UserId == notificationToUpdate.UserId && n.OrganizationId == notificationToUpdate.OrganizationId &&
                n.CreationDate == notificationToUpdate.CreationDate &&
                // Updated fields
                n.Priority == notificationToUpdate.Priority && n.ClientType == notificationToUpdate.ClientType &&
                n.Title == notificationToUpdate.Title && n.Body == notificationToUpdate.Body &&
                DateTime.UtcNow - n.RevisionDate < TimeSpan.FromMinutes(1)));
    }
}
