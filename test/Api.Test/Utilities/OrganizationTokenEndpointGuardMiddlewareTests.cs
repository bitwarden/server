using System.Security.Claims;
using Bit.Api.Utilities;
using Bit.Core.Auth.Identity;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Bit.Api.Test.Utilities;

public class OrganizationTokenEndpointGuardMiddlewareTests
{
    private const string OrganizationClientId = "organization.3f1b2c4d-0000-0000-0000-000000000001";

    [Fact]
    public async Task OrganizationToken_EndpointWithoutOrganizationScopePolicy_ReturnsForbidden()
    {
        var (context, nextCalled) = await InvokeAsync(OrganizationClientId, new AuthorizeAttribute(Policies.Application));

        Assert.False(nextCalled());
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task OrganizationToken_EndpointWithoutAuthorizationMetadata_ReturnsForbidden()
    {
        var (context, nextCalled) = await InvokeAsync(OrganizationClientId);

        Assert.False(nextCalled());
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task OrganizationToken_EndpointWithOrganizationPolicy_CallsNext()
    {
        var (_, nextCalled) = await InvokeAsync(OrganizationClientId, new AuthorizeAttribute(Policies.Organization));

        Assert.True(nextCalled());
    }

    [Theory]
    [InlineData(Policies.OrganizationMembersRead)]
    [InlineData(Policies.OrganizationMembersWrite)]
    [InlineData(Policies.OrganizationGroupsRead)]
    [InlineData(Policies.OrganizationGroupsWrite)]
    [InlineData(Policies.OrganizationCollectionsRead)]
    [InlineData(Policies.OrganizationCollectionsWrite)]
    [InlineData(Policies.OrganizationPoliciesRead)]
    [InlineData(Policies.OrganizationEventsRead)]
    [InlineData(Policies.OrganizationSubscriptionRead)]
    [InlineData(Policies.OrganizationSubscriptionWrite)]
    public async Task OrganizationToken_EndpointWithScopedOrganizationPolicy_CallsNext(string policy)
    {
        var (_, nextCalled) = await InvokeAsync(OrganizationClientId, new AuthorizeAttribute(policy));

        Assert.True(nextCalled());
    }

    [Fact]
    public async Task OrganizationToken_AllowAnonymousEndpoint_CallsNext()
    {
        var (_, nextCalled) = await InvokeAsync(OrganizationClientId, new AllowAnonymousAttribute());

        Assert.True(nextCalled());
    }

    [Theory]
    [InlineData("web")]
    [InlineData("user.3f1b2c4d-0000-0000-0000-000000000002")]
    [InlineData(null)]
    public async Task NonOrganizationToken_EndpointWithoutOrganizationScopePolicy_CallsNext(string? clientId)
    {
        var (_, nextCalled) = await InvokeAsync(clientId, new AuthorizeAttribute(Policies.Application));

        Assert.True(nextCalled());
    }

    private static async Task<(HttpContext Context, Func<bool> NextCalled)> InvokeAsync(
        string? clientId, params object[] endpointMetadata)
    {
        var claims = clientId == null ? [] : new[] { new Claim(JwtClaimTypes.ClientId, clientId) };
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")),
        };
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(endpointMetadata), "test"));

        var called = false;
        var middleware = new OrganizationTokenEndpointGuardMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);
        return (context, () => called);
    }
}
