#nullable enable
using Bit.Core.Context;
using Bit.Core.Test.NotificationCenter.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.NotificationCenter.Authorization;

using System.Security.Claims;
using Bit.Core.NotificationCenter.Authorization;
using Bit.Core.NotificationCenter.Entities;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

[SutProviderCustomize]
[NotificationStatusCustomize]
public class NotificationStatusAuthorizationHandlerTests
{
    private static void SetupUserPermission(
        SutProvider<NotificationStatusAuthorizationHandler> sutProvider,
        Guid? userId = null
    )
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAsync_UnsupportedNotificationOperationRequirement_Throws(
        SutProvider<NotificationStatusAuthorizationHandler> sutProvider,
        NotificationStatus notificationStatus,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, Guid.NewGuid());
        var requirement = new NotificationStatusOperationsRequirement("UnsupportedOperation");
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notificationStatus
        );

        await Assert.ThrowsAsync<ArgumentException>(() => sutProvider.Sut.HandleAsync(context));
    }

    [Theory]
    [BitAutoData(nameof(NotificationStatusOperations.Read))]
    [BitAutoData(nameof(NotificationStatusOperations.Create))]
    [BitAutoData(nameof(NotificationStatusOperations.Update))]
    public async Task HandleAsync_NotLoggedIn_Unauthorized(
        string requirementName,
        SutProvider<NotificationStatusAuthorizationHandler> sutProvider,
        NotificationStatus notificationStatus,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, userId: null);
        var requirement = new NotificationStatusOperationsRequirement(requirementName);
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notificationStatus
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(nameof(NotificationStatusOperations.Read))]
    [BitAutoData(nameof(NotificationStatusOperations.Create))]
    [BitAutoData(nameof(NotificationStatusOperations.Update))]
    public async Task HandleAsync_ResourceEmpty_Unauthorized(
        string requirementName,
        SutProvider<NotificationStatusAuthorizationHandler> sutProvider,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, Guid.NewGuid());
        var requirement = new NotificationStatusOperationsRequirement(requirementName);
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            null
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAsync_ReadRequirementUserNotMatching_Unauthorized(
        SutProvider<NotificationStatusAuthorizationHandler> sutProvider,
        NotificationStatus notificationStatus,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, Guid.NewGuid());

        var requirement = NotificationStatusOperations.Read;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notificationStatus
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAsync_ReadRequirement_Authorized(
        SutProvider<NotificationStatusAuthorizationHandler> sutProvider,
        NotificationStatus notificationStatus,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, notificationStatus.UserId);

        var requirement = NotificationStatusOperations.Read;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notificationStatus
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAsync_CreateRequirementUserNotMatching_Unauthorized(
        SutProvider<NotificationStatusAuthorizationHandler> sutProvider,
        NotificationStatus notificationStatus,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, Guid.NewGuid());

        var requirement = NotificationStatusOperations.Create;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notificationStatus
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAsync_CreateRequirement_Authorized(
        SutProvider<NotificationStatusAuthorizationHandler> sutProvider,
        NotificationStatus notificationStatus,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, notificationStatus.UserId);

        var requirement = NotificationStatusOperations.Create;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notificationStatus
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAsync_UpdateRequirementUserNotMatching_Unauthorized(
        SutProvider<NotificationStatusAuthorizationHandler> sutProvider,
        NotificationStatus notificationStatus,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, Guid.NewGuid());

        var requirement = NotificationStatusOperations.Update;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notificationStatus
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAsync_UpdateRequirement_Authorized(
        SutProvider<NotificationStatusAuthorizationHandler> sutProvider,
        NotificationStatus notificationStatus,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, notificationStatus.UserId);

        var requirement = NotificationStatusOperations.Update;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notificationStatus
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }
}
