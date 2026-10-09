using System.Security.Claims;
using Bit.Api.AdminConsole.Public.Controllers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.IdentityServer;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Public.Controllers;

/// <summary>
/// Evaluates each public Admin Console action's combined authorization metadata, as the authorization middleware does.
/// </summary>
public class PublicApiScopePolicyTests : IClassFixture<ApiApplicationFactory>
{
    private static readonly Type[] _controllers =
    [
        typeof(MembersController),
        typeof(GroupsController),
        typeof(CollectionsController),
        typeof(PoliciesController),
        typeof(OrganizationController),
    ];

    private readonly ApiApplicationFactory _factory;

    public PublicApiScopePolicyTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<Type, string, string[]> ExpectedPolicies => new()
    {
        { typeof(MembersController), nameof(MembersController.Get), [Policies.OrganizationMembersRead] },
        { typeof(MembersController), nameof(MembersController.GetGroupIds), [Policies.OrganizationMembersRead] },
        { typeof(MembersController), nameof(MembersController.List), [Policies.OrganizationMembersRead] },
        { typeof(MembersController), nameof(MembersController.Post), [Policies.OrganizationMembersWrite] },
        { typeof(MembersController), nameof(MembersController.Put), [Policies.OrganizationMembersWrite] },
        { typeof(MembersController), nameof(MembersController.PutGroupIds), [Policies.OrganizationMembersWrite] },
        { typeof(MembersController), nameof(MembersController.Remove), [Policies.OrganizationMembersWrite] },
        { typeof(MembersController), nameof(MembersController.PostReinvite), [Policies.OrganizationMembersWrite] },
        { typeof(MembersController), nameof(MembersController.Revoke), [Policies.OrganizationMembersWrite] },
        { typeof(MembersController), nameof(MembersController.Restore), [Policies.OrganizationMembersWrite] },
        { typeof(GroupsController), nameof(GroupsController.Get), [Policies.OrganizationGroupsRead] },
        { typeof(GroupsController), nameof(GroupsController.GetMemberIds), [Policies.OrganizationGroupsRead] },
        { typeof(GroupsController), nameof(GroupsController.List), [Policies.OrganizationGroupsRead] },
        { typeof(GroupsController), nameof(GroupsController.Post), [Policies.OrganizationGroupsWrite] },
        { typeof(GroupsController), nameof(GroupsController.Put), [Policies.OrganizationGroupsWrite] },
        { typeof(GroupsController), nameof(GroupsController.PutMemberIds), [Policies.OrganizationGroupsWrite] },
        { typeof(GroupsController), nameof(GroupsController.Delete), [Policies.OrganizationGroupsWrite] },
        { typeof(CollectionsController), nameof(CollectionsController.Get), [Policies.OrganizationCollectionsRead] },
        { typeof(CollectionsController), nameof(CollectionsController.List), [Policies.OrganizationCollectionsRead] },
        { typeof(CollectionsController), nameof(CollectionsController.Put), [Policies.OrganizationCollectionsWrite] },
        { typeof(CollectionsController), nameof(CollectionsController.Delete), [Policies.OrganizationCollectionsWrite] },
        { typeof(PoliciesController), nameof(PoliciesController.Get), [Policies.OrganizationPoliciesRead] },
        { typeof(PoliciesController), nameof(PoliciesController.List), [Policies.OrganizationPoliciesRead] },
        { typeof(PoliciesController), nameof(PoliciesController.Put), [Policies.Organization] },
        {
            typeof(OrganizationController), nameof(OrganizationController.Import),
            [Policies.OrganizationGroupsWrite, Policies.OrganizationMembersWrite]
        },
    };

    public static TheoryData<string> MembersWriteActions => new()
    {
        nameof(MembersController.Post),
        nameof(MembersController.Put),
        nameof(MembersController.PutGroupIds),
        nameof(MembersController.Remove),
        nameof(MembersController.PostReinvite),
        nameof(MembersController.Revoke),
        nameof(MembersController.Restore),
    };

    [Theory]
    [MemberData(nameof(ExpectedPolicies))]
    public void Action_RequiresExpectedPolicies(Type controller, string action, string[] expectedPolicies)
    {
        var policies = GetAction(controller, action).EndpointMetadata
            .OfType<IAuthorizeData>()
            .Select(d => d.Policy)
            .Order();

        Assert.Equal(expectedPolicies.Order(), policies);
    }

    [Fact]
    public void EveryAction_HasAnExpectedPolicyEntry()
    {
        var mapped = ExpectedPolicies.Select(row => $"{((Type)row[0]).Name}.{row[1]}").ToHashSet();

        var unmapped = GetActions()
            .Select(a => $"{a.ControllerTypeInfo.Name}.{a.MethodInfo.Name}")
            .Where(name => !mapped.Contains(name))
            .ToList();

        Assert.Empty(unmapped);
    }

    [Theory]
    [MemberData(nameof(ExpectedPolicies))]
    public async Task LegacyOrganizationScope_IsAuthorizedForEveryAction(Type controller, string action, string[] _)
    {
        var result = await AuthorizeActionAsync(controller, action, ApiScopes.ApiOrganization);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [MemberData(nameof(MembersWriteActions))]
    public async Task MembersReadScope_MembersWriteAction_IsForbidden(string action)
    {
        var result = await AuthorizeActionAsync(typeof(MembersController), action,
            ApiScopes.ApiOrganizationMembersRead);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [MemberData(nameof(MembersWriteActions))]
    public async Task MembersWriteScope_MembersWriteAction_IsAuthorized(string action)
    {
        var result = await AuthorizeActionAsync(typeof(MembersController), action,
            ApiScopes.ApiOrganizationMembersWrite);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task MembersWriteScope_MembersRead_IsForbidden()
    {
        var result = await AuthorizeActionAsync(typeof(MembersController), nameof(MembersController.List),
            ApiScopes.ApiOrganizationMembersWrite);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task PoliciesReadScope_PutPolicy_IsForbidden()
    {
        var result = await AuthorizeActionAsync(typeof(PoliciesController), nameof(PoliciesController.Put),
            ApiScopes.ApiOrganizationPoliciesRead);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(ApiScopes.ApiOrganizationMembersWrite)]
    [InlineData(ApiScopes.ApiOrganizationGroupsWrite)]
    public async Task SingleWriteScope_Import_IsForbidden(string scope)
    {
        var result = await AuthorizeActionAsync(typeof(OrganizationController), nameof(OrganizationController.Import),
            scope);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task MembersAndGroupsWriteScopes_Import_IsAuthorized()
    {
        var result = await AuthorizeActionAsync(typeof(OrganizationController), nameof(OrganizationController.Import),
            ApiScopes.ApiOrganizationMembersWrite, ApiScopes.ApiOrganizationGroupsWrite);

        Assert.True(result.Succeeded);
    }

    private async Task<AuthorizationResult> AuthorizeActionAsync(Type controller, string action, params string[] scopes)
    {
        var policy = await AuthorizationPolicy.CombineAsync(
            _factory.GetService<IAuthorizationPolicyProvider>(),
            GetAction(controller, action).EndpointMetadata.OfType<IAuthorizeData>());
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            scopes.Select(s => new Claim(JwtClaimTypes.Scope, s)), "Bearer"));

        return await _factory.GetService<IAuthorizationService>().AuthorizeAsync(principal, policy!);
    }

    private ControllerActionDescriptor GetAction(Type controller, string action) =>
        GetActions().Single(a => a.ControllerTypeInfo.AsType() == controller && a.MethodInfo.Name == action);

    private IEnumerable<ControllerActionDescriptor> GetActions() =>
        _factory.GetService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(a => _controllers.Contains(a.ControllerTypeInfo.AsType()));
}
