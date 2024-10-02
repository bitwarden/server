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
public class CreateNotificationCommandTest
{
    private static void Setup(SutProvider<CreateNotificationCommand> sutProvider,
        Notification notification, bool authorized = false)
    {
        sutProvider.GetDependency<INotificationRepository>()
            .CreateAsync(notification)
            .Returns(notification);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), notification,
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(NotificationOperations.Create)))
            .Returns(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_AuthorizationFailed_NotFoundException(
        SutProvider<CreateNotificationCommand> sutProvider,
        Notification notification)
    {
        Setup(sutProvider, notification, authorized: false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CreateAsync(notification));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_Authorized_NotificationCreated(
        SutProvider<CreateNotificationCommand> sutProvider,
        Notification notification)
    {
        Setup(sutProvider, notification, true);

        var newNotification = await sutProvider.Sut.CreateAsync(notification);

        Assert.Equal(notification, newNotification);
        Assert.Equal(DateTime.UtcNow, notification.CreationDate, TimeSpan.FromMinutes(1));
        Assert.Equal(notification.CreationDate, notification.RevisionDate);
    }
}
