using System.Net;
using System.Net.Http.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.IntegrationTest;

/// <summary>
/// Authorization for the <c>organizations/{orgId}/access-connectors</c> admin surface, exercised over the real
/// request pipeline.
/// </summary>
/// <remarks>
/// The endpoint-registration tests in Pam.Test assert that ManageAccessConnectorRequirement is attached to every
/// connector admin route, but they stop before the pipeline runs — presence in metadata is not enforcement. These tests
/// deliberately assert only that a caller was or was not denied, never what the handler returned.
/// <para>
/// One route per group is enough: the requirement is applied once, to the parent group, so a route that escapes it
/// escapes it for the whole group.
/// </para>
/// </remarks>
public class AccessConnectorAuthorizationTests(ApiApplicationFactory factory)
    : AccessRuleIntegrationTestBase(factory, "pam-connector-authz")
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // The connector groups sit behind their own flag, on top of the base PAM flag the harness already
        // enables.
        FeatureService.IsEnabled(FeatureFlagKeys.PamAccessConnector).Returns(true);
    }

    // The connector fleet sits at the group root; rotation's target systems and configs hang beneath it.
    private string ConnectorUrl(string resource) =>
        $"organizations/{Organization.Id}/access-connectors{resource}";

    [Theory]
    [InlineData("")]
    [InlineData("/rotation/target-systems")]
    [InlineData("/rotation/configs")]
    public async Task Read_AsNonMember_ReturnsForbidden(string resource)
    {
        var outsiderEmail = $"outsider-{Guid.NewGuid()}@bitwarden.com";
        await Factory.LoginWithNewAccount(outsiderEmail);
        await LoginHelper.LoginAsync(outsiderEmail);

        var response = await Client.GetAsync(ConnectorUrl(resource));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/rotation/target-systems")]
    [InlineData("/rotation/configs")]
    public async Task Read_AsPlainMember_ReturnsForbidden(string resource)
    {
        var (memberEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            Organization.Id, OrganizationUserType.User);
        await LoginHelper.LoginAsync(memberEmail);

        var response = await Client.GetAsync(ConnectorUrl(resource));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/rotation/target-systems")]
    [InlineData("/rotation/configs")]
    public async Task Read_AsCustomUserWithManageAccessRules_ReturnsForbidden(string resource)
    {
        // ManageAccessRules is authority over who may lease a credential, not over the access connectors
        // that rotate it.
        var (customEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            Organization.Id, OrganizationUserType.Custom, new Permissions { ManageAccessRules = true });
        await LoginHelper.LoginAsync(customEmail);

        var response = await Client.GetAsync(ConnectorUrl(resource));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/rotation/target-systems")]
    [InlineData("/rotation/configs")]
    public async Task Read_AsProviderUserForTheOrganization_ReturnsForbidden(string resource)
    {
        // A registered access connector is handed the organization key, which is not a provider's to hold.
        await LoginAsProviderForOrganizationAsync();

        var response = await Client.GetAsync(ConnectorUrl(resource));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Write_AsPlainMember_ReturnsForbidden()
    {
        var (memberEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            Organization.Id, OrganizationUserType.User);
        await LoginHelper.LoginAsync(memberEmail);

        var response = await Client.PostAsJsonAsync(ConnectorUrl(""), new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/rotation/target-systems")]
    [InlineData("/rotation/configs")]
    public async Task Read_AsOwner_IsNotForbidden(string resource)
    {
        // Guards against the requirement over-denying.
        await LoginHelper.LoginAsync(OwnerEmail);

        var response = await Client.GetAsync(ConnectorUrl(resource));

        AssertReachedTheHandler(response);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/rotation/target-systems")]
    [InlineData("/rotation/configs")]
    public async Task Read_AsAdmin_IsNotForbidden(string resource)
    {
        var (adminEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            Organization.Id, OrganizationUserType.Admin);
        await LoginHelper.LoginAsync(adminEmail);

        var response = await Client.GetAsync(ConnectorUrl(resource));

        AssertReachedTheHandler(response);
    }

    [Fact]
    public async Task Write_AsOwner_IsNotForbidden()
    {
        await LoginHelper.LoginAsync(OwnerEmail);

        var response = await Client.PostAsJsonAsync(ConnectorUrl(""), new { });

        AssertReachedTheHandler(response);
    }

    /// <summary>
    /// Asserts a caller got past authorization without pinning what the handler did. NotFound is excluded as well as
    /// Forbidden: without it these would still pass if the connector feature gate silently swallowed the route,
    /// which
    /// would in turn make every denial above pass for the wrong reason.
    /// </summary>
    private static void AssertReachedTheHandler(HttpResponseMessage response)
    {
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

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
