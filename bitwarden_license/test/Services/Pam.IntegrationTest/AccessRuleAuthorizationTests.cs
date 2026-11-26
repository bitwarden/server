using System.Net;
using System.Net.Http.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Xunit;

namespace Bit.Services.Pam.IntegrationTest;

/// <summary>
/// Authorization for <c>organizations/{orgId}/access-rules</c> over the real request pipeline. The Pam.Test
/// registration tests only prove the requirements are attached, not enforced.
/// </summary>
public class AccessRuleAuthorizationTests(ApiApplicationFactory factory)
    : AccessRuleIntegrationTestBase(factory, "pam-access-rule-authz")
{
    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task AnyRoute_AsNonMember_ReturnsForbidden(string method)
    {
        var outsiderEmail = $"outsider-{Guid.NewGuid()}@bitwarden.com";
        await Factory.LoginWithNewAccount(outsiderEmail);
        await LoginHelper.LoginAsync(outsiderEmail);

        var response = await SendAsync(method);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task AnyRoute_AsPlainMember_ReturnsForbidden(string method)
    {
        var (memberEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            Organization.Id, OrganizationUserType.User);
        await LoginHelper.LoginAsync(memberEmail);

        var response = await SendAsync(method);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task AnyRoute_AsCustomUserWithoutManageAccessRules_ReturnsForbidden(string method)
    {
        var (customEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            Organization.Id, OrganizationUserType.Custom, new Permissions { ManageAccessRules = false });
        await LoginHelper.LoginAsync(customEmail);

        var response = await SendAsync(method);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task AnyRoute_AsProviderUserForTheOrganization_ReturnsForbidden(string method)
    {
        await LoginAsProviderForOrganizationAsync();

        var response = await SendAsync(method);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task ListOrCreate_AsCustomUserWithManageAccessRules_IsNotForbidden(string method)
    {
        var (customEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            Organization.Id, OrganizationUserType.Custom, new Permissions { ManageAccessRules = true });
        await LoginHelper.LoginAsync(customEmail);

        var response = await SendAsync(method);

        AssertReachedTheHandler(response);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task ListOrCreate_AsOwner_IsNotForbidden(string method)
    {
        await LoginHelper.LoginAsync(OwnerEmail);

        var response = await SendAsync(method);

        AssertReachedTheHandler(response);
    }

    /// <summary>
    /// Also rejects NotFound, so a feature gate swallowing the route does not pass as authorized. Only the list and
    /// create routes can show that: the by-id routes target a rule that doesn't exist.
    /// </summary>
    private static void AssertReachedTheHandler(HttpResponseMessage response)
    {
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    public static TheoryData<string> AllRoutes => new() { "GET", "GET {id}", "POST", "PUT", "DELETE" };

    private Task<HttpResponseMessage> SendAsync(string method) => method switch
    {
        "GET" => Client.GetAsync(AccessRulesUrl),
        "GET {id}" => Client.GetAsync(AccessRuleUrl(Guid.NewGuid())),
        "POST" => Client.PostAsJsonAsync(AccessRulesUrl, new { }),
        "PUT" => Client.PutAsJsonAsync(AccessRuleUrl(Guid.NewGuid()), new { }),
        "DELETE" => Client.DeleteAsync(AccessRuleUrl(Guid.NewGuid())),
        _ => throw new ArgumentOutOfRangeException(nameof(method))
    };
}
