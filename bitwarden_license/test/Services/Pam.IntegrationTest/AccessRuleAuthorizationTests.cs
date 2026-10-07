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
    [Fact]
    public async Task Read_AsNonMember_ReturnsForbidden()
    {
        var outsiderEmail = $"outsider-{Guid.NewGuid()}@bitwarden.com";
        await Factory.LoginWithNewAccount(outsiderEmail);
        await LoginHelper.LoginAsync(outsiderEmail);

        var response = await Client.GetAsync(AccessRulesUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Write_AsNonMember_ReturnsForbidden(string method)
    {
        var outsiderEmail = $"outsider-{Guid.NewGuid()}@bitwarden.com";
        await Factory.LoginWithNewAccount(outsiderEmail);
        await LoginHelper.LoginAsync(outsiderEmail);

        var response = await SendWriteAsync(method);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Write_AsPlainMember_ReturnsForbidden(string method)
    {
        var (memberEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            Organization.Id, OrganizationUserType.User);
        await LoginHelper.LoginAsync(memberEmail);

        var response = await SendWriteAsync(method);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Write_AsCustomUserWithoutManageAccessRules_ReturnsForbidden(string method)
    {
        var (customEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            Organization.Id, OrganizationUserType.Custom, new Permissions { ManageAccessRules = false });
        await LoginHelper.LoginAsync(customEmail);

        var response = await SendWriteAsync(method);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Read_AsProviderUserForTheOrganization_ReturnsForbidden()
    {
        // Access rules gate who can lease credentials, so the group uses MemberRequirement, not
        // MemberOrProviderRequirement.
        await LoginAsProviderForOrganizationAsync();

        var response = await Client.GetAsync(AccessRulesUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Write_AsProviderUserForTheOrganization_ReturnsForbidden()
    {
        await LoginAsProviderForOrganizationAsync();

        var response = await SendWriteAsync("POST");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Read_AsMember_IsNotForbidden()
    {
        var (memberEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            Organization.Id, OrganizationUserType.User);
        await LoginHelper.LoginAsync(memberEmail);

        var response = await Client.GetAsync(AccessRulesUrl);

        AssertReachedTheHandler(response);
    }

    [Fact]
    public async Task Write_AsOwner_IsNotForbidden()
    {
        await LoginHelper.LoginAsync(OwnerEmail);

        var response = await SendWriteAsync("POST");

        AssertReachedTheHandler(response);
    }

    /// <summary>Also rejects NotFound, so a feature gate swallowing the route does not pass as authorized.</summary>
    private static void AssertReachedTheHandler(HttpResponseMessage response)
    {
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<HttpResponseMessage> SendWriteAsync(string method) => method switch
    {
        "POST" => Client.PostAsJsonAsync(AccessRulesUrl, new { }),
        "PUT" => Client.PutAsJsonAsync(AccessRuleUrl(Guid.NewGuid()), new { }),
        "DELETE" => Client.DeleteAsync(AccessRuleUrl(Guid.NewGuid())),
        _ => throw new ArgumentOutOfRangeException(nameof(method))
    };
}
