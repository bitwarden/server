#nullable enable
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.NotificationCenter.Queries;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Test.NotificationCenter.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.NotificationCenter.Queries;

using System.Security.Claims;
using Bit.Core.NotificationCenter.Authorization;
using Bit.Core.NotificationCenter.Entities;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

[SutProviderCustomize]
[NotificationStatusCustomize]
public class GetNotificationStatusForUserQueryTest
{
    private static void Setup(SutProvider<GetNotificationStatusForUserQuery> sutProvider,
        Guid notificationId, NotificationStatus? notificationStatus, Guid? userId, bool authorized = false)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<INotificationStatusRepository>()
            .GetByNotificationIdAndUserIdAsync(notificationId, userId.GetValueOrDefault(Guid.NewGuid()))
            .Returns(notificationStatus);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), notificationStatus ?? Arg.Any<NotificationStatus>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(NotificationStatusOperations.Read)))
            .Returns(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());
    }

    [Theory]
    [BitAutoData]
    public async Task GetByUserIdStatusFilterAsync_UserNotLoggedIn_NotFoundException(
        SutProvider<GetNotificationStatusForUserQuery> sutProvider,
        Guid notificationId, NotificationStatus notificationStatus)
    {
        Setup(sutProvider, notificationId, notificationStatus, userId: null, true);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetByNotificationIdAndUserIdAsync(notificationId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetByUserIdStatusFilterAsync_NotificationStatusNotFound_NotFoundException(
        SutProvider<GetNotificationStatusForUserQuery> sutProvider,
        Guid notificationId)
    {
        Setup(sutProvider, notificationId, notificationStatus: null, Guid.NewGuid(), true);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetByNotificationIdAndUserIdAsync(notificationId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetByUserIdStatusFilterAsync_AuthorizationFailed_NotFoundException(
        SutProvider<GetNotificationStatusForUserQuery> sutProvider,
        Guid notificationId, NotificationStatus notificationStatus)
    {
        Setup(sutProvider, notificationId, notificationStatus, Guid.NewGuid(), authorized: false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetByNotificationIdAndUserIdAsync(notificationId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetByUserIdStatusFilterAsync_NotificationFoundAuthorized_Returned(
        SutProvider<GetNotificationStatusForUserQuery> sutProvider,
        Guid notificationId, NotificationStatus notificationStatus)
    {
        Setup(sutProvider, notificationId, notificationStatus, Guid.NewGuid(), true);

        var actualNotificationStatus = await sutProvider.Sut.GetByNotificationIdAndUserIdAsync(notificationId);

        Assert.Equal(notificationStatus, actualNotificationStatus);
    }
}
