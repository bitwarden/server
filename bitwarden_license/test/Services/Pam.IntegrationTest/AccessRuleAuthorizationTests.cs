using System.Net;
using System.Net.Http.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Services.Pam.IntegrationTest;

/// <summary>
/// Authorization for <c>organizations/{orgId}/access-rules</c>, exercised over the real request pipeline.
/// </summary>
/// <remarks>
/// The endpoint-registration tests in Pam.Test assert which requirements are attached to which route, but they stop
/// before the pipeline runs — presence in metadata is not enforcement. These tests deliberately assert only that a
/// caller was or was not denied, never what the handler returned; the round-trip behaviour is
/// <see cref="AccessRuleCrudTests"/>'s subject.
/// </remarks>
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
        // Providers manage an organization's billing and configuration, but access rules gate who can lease
        // credentials out of it. The group deliberately uses MemberRequirement, not MemberOrProviderRequirement.
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
        // Guards against the group requirement over-denying: reading rules is available to any member.
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

    /// <summary>
    /// Asserts a caller got past authorization without pinning what the handler did with a deliberately empty body.
    /// NotFound is excluded as well as Forbidden: without it these would still pass if the PAM feature gate silently
    /// swallowed the route, which would in turn make every denial above pass for the wrong reason.
    /// </summary>
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

    private async Task LoginAsProviderForOrganizationAsync()
    {
        var providerEmail = $"provider-{Guid.NewGuid()}@bitwarden.com";
        await Factory.LoginWithNewAccount(providerEmail);

        await Factory.GetService<ICreateProviderCommand>()
            .CreateBusinessUnitAsync(
                new Provider { Name = "provider", Type = ProviderType.BusinessUnit },
                providerEmail,
                PlanType.EnterpriseAnnually2023,
                10);

        var providerUserAccount = await Factory.GetService<IUserRepository>().GetByEmailAsync(providerEmail);
        var providerUser = (await Factory.GetService<IProviderUserRepository>()
            .GetManyByUserAsync(providerUserAccount!.Id)).First();

        await Factory.GetService<IProviderOrganizationRepository>().CreateAsync(new ProviderOrganization
        {
            ProviderId = providerUser.ProviderId,
            OrganizationId = Organization.Id,
            Key = null,
            Settings = null
        });

        await LoginHelper.LoginAsync(providerEmail);
    }
}
