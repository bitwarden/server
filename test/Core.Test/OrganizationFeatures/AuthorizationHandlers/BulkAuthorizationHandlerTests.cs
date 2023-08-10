using System.Security.Claims;
using Bit.Core.OrganizationFeatures.AuthorizationHandlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.AuthorizationHandlers;

public class BulkAuthorizationHandlerTests
{
    [Fact]
    public async Task HandleRequirementAsync_SingleResource_Success()
    {
        var handler = new TestBulkAuthorizationHandler();
        var context = new AuthorizationHandlerContext(
            new[] { new TestOperationRequirement() },
            new ClaimsPrincipal(),
            new TestResource());
        await handler.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_BulkResource_Success()
    {
        var handler = new TestBulkAuthorizationHandler();
        var context = new AuthorizationHandlerContext(
            new[] { new TestOperationRequirement() },
            new ClaimsPrincipal(),
            new[] { new TestResource(), new TestResource() });
        await handler.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_NoResources_Failure()
    {
        var handler = new TestBulkAuthorizationHandler();
        var context = new AuthorizationHandlerContext(
            new[] { new TestOperationRequirement() },
            new ClaimsPrincipal(),
            null);
        await handler.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WrongResourceType_Failure()
    {
        var handler = new TestBulkAuthorizationHandler();
        var context = new AuthorizationHandlerContext(
            new[] { new TestOperationRequirement() },
            new ClaimsPrincipal(),
            new object());
        await handler.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    private class TestOperationRequirement : OperationAuthorizationRequirement { }

    private class TestResource { }

    private class TestBulkAuthorizationHandler : BulkAuthorizationHandler<TestOperationRequirement, TestResource>
    {
        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
            TestOperationRequirement requirement,
            ICollection<TestResource> resources)
        {
            context.Succeed(requirement);
        }
    }
}
