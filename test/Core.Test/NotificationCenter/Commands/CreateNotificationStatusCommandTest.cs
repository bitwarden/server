#nullable enable
using System.Security.Claims;
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
public class CreateNotificationStatusCommandTest
{
    private static void Setup(SutProvider<CreateNotificationStatusCommand> sutProvider,
        Notification? notification, NotificationStatus notificationStatus,
        bool authorizedNotification = false, bool authorizedCreate = false)
    {
        sutProvider.GetDependency<INotificationRepository>()
            .GetByIdAsync(notificationStatus.NotificationId)
            .Returns(notification);
        sutProvider.GetDependency<INotificationStatusRepository>()
            .CreateAsync(notificationStatus)
            .Returns(notificationStatus);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), notification ?? Arg.Any<Notification>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(NotificationOperations.Read)))
            .Returns(authorizedNotification ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), notificationStatus,
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(NotificationStatusOperations.Create)))
            .Returns(authorizedCreate ? AuthorizationResult.Success() : AuthorizationResult.Failed());
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_NotificationNotFound_NotFoundException(
        SutProvider<CreateNotificationStatusCommand> sutProvider,
        NotificationStatus notificationStatus)
    {
        Setup(sutProvider, notification: null, notificationStatus, true, true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CreateAsync(notificationStatus));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_NotificationReadNotAuthorized_NotFoundException(
        SutProvider<CreateNotificationStatusCommand> sutProvider,
        Notification notification, NotificationStatus notificationStatus)
    {
        Setup(sutProvider, notification, notificationStatus, authorizedNotification: false, true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CreateAsync(notificationStatus));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_CreateNotAuthorized_NotFoundException(
        SutProvider<CreateNotificationStatusCommand> sutProvider,
        Notification notification, NotificationStatus notificationStatus)
    {
        Setup(sutProvider, notification, notificationStatus, true, authorizedCreate: false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CreateAsync(notificationStatus));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_NotificationFoundAuthorized_NotificationStatusCreated(
        SutProvider<CreateNotificationStatusCommand> sutProvider,
        Notification notification, NotificationStatus notificationStatus)
    {
        Setup(sutProvider, notification, notificationStatus, true, true);

        var newNotificationStatus = await sutProvider.Sut.CreateAsync(notificationStatus);

        Assert.Equal(notificationStatus, newNotificationStatus);
    }
}
