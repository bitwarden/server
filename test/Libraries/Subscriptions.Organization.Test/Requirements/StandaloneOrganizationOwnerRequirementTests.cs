using System.Security.Claims;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Identity;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Subscriptions.Organization.Requirements;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.Subscriptions.Organization.Test.Requirements;

[SutProviderCustomize]
public class StandaloneOrganizationOwnerRequirementTests
{
    [Theory, BitAutoData]
    public async Task HandleAsync_OwnerOfStandaloneOrg_Succeeds(
        Guid organizationId, Guid userId,
        SutProvider<StandaloneOrganizationOwnerRequirementHandler> sutProvider)
    {
        var context = Arrange(sutProvider, organizationId, userId, OrganizationUserType.Owner, managedByProvider: false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    // The provider-organization lookup spans every org the user belongs to, so a membership in another,
    // provider-managed organization must not deny this route's standalone organization.
    [Theory, BitAutoData]
    public async Task HandleAsync_OwnerOfStandaloneOrgMemberOfOtherManagedOrg_Succeeds(
        Guid organizationId, Guid otherOrganizationId, Guid userId,
        SutProvider<StandaloneOrganizationOwnerRequirementHandler> sutProvider)
    {
        var httpContext = HttpContextFor(organizationId, OrganizationUserType.Owner);
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext = httpContext;
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        sutProvider.GetDependency<IProviderOrganizationRepository>()
            .GetManyByUserAsync(userId)
            .Returns([new ProviderOrganizationProviderDetails { OrganizationId = otherOrganizationId }]);
        var context = Context(httpContext.User);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_OwnerOfManagedOrg_DoesNotSucceed(
        Guid organizationId, Guid userId,
        SutProvider<StandaloneOrganizationOwnerRequirementHandler> sutProvider)
    {
        var context = Arrange(sutProvider, organizationId, userId, OrganizationUserType.Owner, managedByProvider: true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task HandleAsync_NonOwnerMember_DoesNotSucceed(
        OrganizationUserType membership, Guid organizationId, Guid userId,
        SutProvider<StandaloneOrganizationOwnerRequirementHandler> sutProvider)
    {
        var context = Arrange(sutProvider, organizationId, userId, membership, managedByProvider: false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_NoMembership_DoesNotSucceed(
        Guid organizationId, Guid userId,
        SutProvider<StandaloneOrganizationOwnerRequirementHandler> sutProvider)
    {
        var context = Arrange(sutProvider, organizationId, userId, membership: null, managedByProvider: false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_NoHttpContext_Throws(
        SutProvider<StandaloneOrganizationOwnerRequirementHandler> sutProvider)
    {
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext = null;
        var context = Context();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.HandleAsync(context));

        Assert.Contains(StandaloneOrganizationOwnerRequirementHandler.NoHttpContextError, exception.Message);
        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_NoUserId_Throws(
        Guid organizationId,
        SutProvider<StandaloneOrganizationOwnerRequirementHandler> sutProvider)
    {
        var httpContext = HttpContextFor(organizationId, OrganizationUserType.Owner);
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext = httpContext;
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns((Guid?)null);
        var context = Context(httpContext.User);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.HandleAsync(context));

        Assert.Contains(StandaloneOrganizationOwnerRequirementHandler.NoUserIdError, exception.Message);
        Assert.False(context.HasSucceeded);
    }

    private static AuthorizationHandlerContext Arrange(
        SutProvider<StandaloneOrganizationOwnerRequirementHandler> sutProvider,
        Guid organizationId, Guid userId, OrganizationUserType? membership, bool managedByProvider)
    {
        var httpContext = HttpContextFor(organizationId, membership);
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext = httpContext;
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);

        IEnumerable<ProviderOrganizationProviderDetails> providerOrganizations = managedByProvider
            ? [new ProviderOrganizationProviderDetails { OrganizationId = organizationId }]
            : [];
        sutProvider.GetDependency<IProviderOrganizationRepository>()
            .GetManyByUserAsync(userId).Returns(providerOrganizations);

        return Context(httpContext.User);
    }

    private static AuthorizationHandlerContext Context(ClaimsPrincipal? user = null)
        => new([new StandaloneOrganizationOwnerRequirement()], user ?? new ClaimsPrincipal(), resource: null);

    private static DefaultHttpContext HttpContextFor(Guid organizationId, OrganizationUserType? membership)
    {
        var httpContext = new DefaultHttpContext { User = BuildPrincipal(organizationId, membership) };
        httpContext.Request.RouteValues["organizationId"] = organizationId.ToString();
        return httpContext;
    }

    private static ClaimsPrincipal BuildPrincipal(Guid organizationId, OrganizationUserType? membership)
    {
        var identity = new ClaimsIdentity();
        if (membership is not null)
        {
            var claimType = membership switch
            {
                OrganizationUserType.Owner => Claims.OrganizationOwner,
                OrganizationUserType.Admin => Claims.OrganizationAdmin,
                OrganizationUserType.Custom => Claims.OrganizationCustom,
                _ => Claims.OrganizationUser
            };
            identity.AddClaim(new Claim(claimType, organizationId.ToString()));
        }

        return new ClaimsPrincipal(identity);
    }
}
