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
[NotificationCustomize]
public class NotificationAuthorizationHandlerTests
{
    private static void SetupUserPermission(
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Guid? userId = null,
        Guid? organizationId = null,
        bool canAccessReports = false
    )
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider
            .GetDependency<ICurrentContext>()
            .GetOrganization(organizationId.GetValueOrDefault(Guid.NewGuid()))
            .Returns(new CurrentContextOrganization());
        sutProvider
            .GetDependency<ICurrentContext>()
            .AccessReports(organizationId.GetValueOrDefault(Guid.NewGuid()))
            .Returns(canAccessReports);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAsync_UnsupportedNotificationOperationRequirement_Throws(
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Notification notification,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, Guid.NewGuid());
        var requirement = new NotificationOperationsRequirement("UnsupportedOperation");
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notification
        );

        await Assert.ThrowsAsync<ArgumentException>(() => sutProvider.Sut.HandleAsync(context));
    }

    [Theory]
    [BitAutoData(nameof(NotificationOperations.Read))]
    [BitAutoData(nameof(NotificationOperations.Create))]
    [BitAutoData(nameof(NotificationOperations.Update))]
    public async Task HandleAsync_NotLoggedIn_Unauthorized(
        string requirementName,
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Notification notification,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, userId: null);
        var requirement = new NotificationOperationsRequirement(requirementName);
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notification
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(nameof(NotificationOperations.Read))]
    [BitAutoData(nameof(NotificationOperations.Create))]
    [BitAutoData(nameof(NotificationOperations.Update))]
    public async Task HandleAsync_ResourceEmpty_Unauthorized(
        string requirementName,
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, Guid.NewGuid());
        var requirement = new NotificationOperationsRequirement(requirementName);
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
    [NotificationCustomize(global: true)]
    public async Task HandleAsync_ReadRequirementGlobalNotification_Authorized(
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Notification notification,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, Guid.NewGuid());

        var requirement = NotificationOperations.Read;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notification
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(false)]
    [BitAutoData(true)]
    [NotificationCustomize(global: false)]
    public async Task HandleAsync_ReadRequirementUserNotMatching_Unauthorized(
        bool hasOrganizationId,
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Notification notification,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, Guid.NewGuid(), notification.OrganizationId);

        if (!hasOrganizationId)
        {
            notification.OrganizationId = null;
        }

        var requirement = NotificationOperations.Read;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notification
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    [BitAutoData(false)]
    [BitAutoData(true)]
    [NotificationCustomize(global: false)]
    public async Task HandleAsync_ReadRequirementOrganizationNotMatching_Unauthorized(
        bool hasUserId,
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Notification notification,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, notification.UserId, Guid.NewGuid());

        if (!hasUserId)
        {
            notification.UserId = null;
        }

        var requirement = NotificationOperations.Read;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notification
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(false, true)]
    [BitAutoData(true, false)]
    [BitAutoData(true, true)]
    [NotificationCustomize(global: false)]
    public async Task HandleAsync_ReadRequirement_Authorized(
        bool hasUserId,
        bool hasOrganizationId,
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Notification notification,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, notification.UserId, notification.OrganizationId);

        if (!hasUserId)
        {
            notification.UserId = null;
        }

        if (!hasOrganizationId)
        {
            notification.OrganizationId = null;
        }

        var requirement = NotificationOperations.Read;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notification
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    [NotificationCustomize(global: true)]
    public async Task HandleAsync_CreateRequirementGlobalNotification_Unauthorized(
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Notification notification,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, Guid.NewGuid());
        var requirement = NotificationOperations.Create;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notification
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    [NotificationCustomize(global: false)]
    public async Task HandleAsync_CreateRequirementUserNotMatching_Unauthorized(
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Notification notification,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, Guid.NewGuid(), notification.OrganizationId);

        notification.OrganizationId = null;

        var requirement = NotificationOperations.Create;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notification
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    [NotificationCustomize(global: false)]
    public async Task HandleAsync_CreateRequirementOrganizationNotMatching_Unauthorized(
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Notification notification,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, notification.UserId, Guid.NewGuid());

        var requirement = NotificationOperations.Create;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notification
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    [NotificationCustomize(global: false)]
    public async Task HandleAsync_CreateRequirementOrganizationUserNoAccessReportsPermission_Unauthorized(
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Notification notification,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(
            sutProvider,
            notification.UserId,
            notification.OrganizationId,
            canAccessReports: false
        );

        var requirement = NotificationOperations.Create;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notification
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    [NotificationCustomize(global: false)]
    public async Task HandleAsync_CreateRequirementUserNotPartOfOrganization_Authorized(
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Notification notification,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, notification.UserId);

        notification.OrganizationId = null;

        var requirement = NotificationOperations.Create;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notification
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(false)]
    [BitAutoData(true)]
    [NotificationCustomize(global: false)]
    public async Task HandleAsync_CreateRequirementOrganizationUserCanAccessReports_Authorized(
        bool hasUserId,
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Notification notification,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, notification.UserId, notification.OrganizationId, true);

        if (!hasUserId)
        {
            notification.UserId = null;
        }

        var requirement = NotificationOperations.Create;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notification
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    // TODO
    [Theory]
    [BitAutoData]
    [NotificationCustomize(global: true)]
    public async Task HandleAsync_UpdateRequirementGlobalNotification_Unauthorized(
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Notification notification,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, Guid.NewGuid());
        var requirement = NotificationOperations.Update;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notification
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    [NotificationCustomize(global: false)]
    public async Task HandleAsync_UpdateRequirementUserNotMatching_Unauthorized(
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Notification notification,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, Guid.NewGuid(), notification.OrganizationId);

        notification.OrganizationId = null;

        var requirement = NotificationOperations.Update;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notification
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    [NotificationCustomize(global: false)]
    public async Task HandleAsync_UpdateRequirementOrganizationNotMatching_Unauthorized(
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Notification notification,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, notification.UserId, Guid.NewGuid());

        var requirement = NotificationOperations.Update;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notification
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    [NotificationCustomize(global: false)]
    public async Task HandleAsync_UpdateRequirementOrganizationUserNoAccessReportsPermission_Unauthorized(
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Notification notification,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(
            sutProvider,
            notification.UserId,
            notification.OrganizationId,
            canAccessReports: false
        );

        var requirement = NotificationOperations.Update;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notification
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    [NotificationCustomize(global: false)]
    public async Task HandleAsync_UpdateRequirementUserNotPartOfOrganization_Authorized(
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Notification notification,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, notification.UserId);

        notification.OrganizationId = null;

        var requirement = NotificationOperations.Update;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notification
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(false)]
    [BitAutoData(true)]
    [NotificationCustomize(global: false)]
    public async Task HandleAsync_UpdateRequirementOrganizationUserCanAccessReports_Authorized(
        bool hasUserId,
        SutProvider<NotificationAuthorizationHandler> sutProvider,
        Notification notification,
        ClaimsPrincipal claimsPrincipal
    )
    {
        SetupUserPermission(sutProvider, notification.UserId, notification.OrganizationId, true);

        if (!hasUserId)
        {
            notification.UserId = null;
        }

        var requirement = NotificationOperations.Update;
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal,
            notification
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }
}
