using System.Security.Claims;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.IdentityServer;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Bit.Api.IntegrationTest.Auth;

public class OrganizationScopedPolicyTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public OrganizationScopedPolicyTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<string, string> ScopedPolicies => new()
    {
        { Policies.OrganizationMembersRead, ApiScopes.ApiOrganizationMembersRead },
        { Policies.OrganizationMembersWrite, ApiScopes.ApiOrganizationMembersWrite },
        { Policies.OrganizationGroupsRead, ApiScopes.ApiOrganizationGroupsRead },
        { Policies.OrganizationGroupsWrite, ApiScopes.ApiOrganizationGroupsWrite },
        { Policies.OrganizationCollectionsRead, ApiScopes.ApiOrganizationCollectionsRead },
        { Policies.OrganizationCollectionsWrite, ApiScopes.ApiOrganizationCollectionsWrite },
        { Policies.OrganizationPoliciesRead, ApiScopes.ApiOrganizationPoliciesRead },
        { Policies.OrganizationEventsRead, ApiScopes.ApiOrganizationEventsRead },
        { Policies.OrganizationSubscriptionRead, ApiScopes.ApiOrganizationSubscriptionRead },
        { Policies.OrganizationSubscriptionWrite, ApiScopes.ApiOrganizationSubscriptionWrite },
    };

    public static TheoryData<string> PoliciesOtherThanEventsRead => new()
    {
        Policies.Organization,
        Policies.OrganizationMembersRead,
        Policies.OrganizationMembersWrite,
        Policies.OrganizationGroupsRead,
        Policies.OrganizationGroupsWrite,
        Policies.OrganizationCollectionsRead,
        Policies.OrganizationCollectionsWrite,
        Policies.OrganizationPoliciesRead,
        Policies.OrganizationSubscriptionRead,
        Policies.OrganizationSubscriptionWrite,
    };

    [Theory]
    [MemberData(nameof(ScopedPolicies))]
    public async Task ScopedPolicy_AcceptsLegacyOrganizationScope(string policy, string _)
    {
        var result = await AuthorizeAsync(ApiScopes.ApiOrganization, policy);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [MemberData(nameof(ScopedPolicies))]
    public async Task ScopedPolicy_AcceptsItsOwnScope(string policy, string scope)
    {
        var result = await AuthorizeAsync(scope, policy);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [MemberData(nameof(PoliciesOtherThanEventsRead))]
    public async Task EventsReadToken_OtherOrganizationPolicies_Fail(string policy)
    {
        var result = await AuthorizeAsync(ApiScopes.ApiOrganizationEventsRead, policy);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [MemberData(nameof(ScopedPolicies))]
    public async Task ScopedPolicy_RejectsUnauthenticatedPrincipal(string policy, string scope)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Scope, scope)]));

        var result = await _factory.GetService<IAuthorizationService>().AuthorizeAsync(principal, policy);

        Assert.False(result.Succeeded);
    }

    private Task<AuthorizationResult> AuthorizeAsync(string scope, string policy)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Scope, scope)], "Bearer"));
        return _factory.GetService<IAuthorizationService>().AuthorizeAsync(principal, policy);
    }
}
