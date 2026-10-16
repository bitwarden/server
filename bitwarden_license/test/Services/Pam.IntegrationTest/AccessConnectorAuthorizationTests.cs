using System.Net;
using System.Net.Http.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.IntegrationTest;

/// <summary>
/// Authorization for the <c>organizations/{orgId}/access-connectors</c> admin surface over the real request pipeline.
/// One route per group is enough, since the requirement is applied to the parent group.
/// </summary>
public class AccessConnectorAuthorizationTests(ApiApplicationFactory factory)
    : AccessRuleIntegrationTestBase(factory, "pam-connector-authz")
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // Connector groups sit behind their own flag instead of the base PAM flag.
        FeatureService.IsEnabled(FeatureFlagKeys.PamAccessConnector).Returns(true);
    }

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
        // ManageAccessRules governs who may lease a credential, not the access connectors that rotate it.
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
    public async Task Read_AsCustomUserWithManageRotation_IsNotForbidden(string resource)
    {
        var (customEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            Organization.Id, OrganizationUserType.Custom, new Permissions { ManageRotation = true });
        await LoginHelper.LoginAsync(customEmail);

        var response = await Client.GetAsync(ConnectorUrl(resource));

        AssertReachedTheHandler(response);
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

    [Fact]
    public async Task Write_AsCustomUserWithManageRotation_IsNotForbidden()
    {
        var (customEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            Organization.Id, OrganizationUserType.Custom, new Permissions { ManageRotation = true });
        await LoginHelper.LoginAsync(customEmail);

        var response = await Client.PostAsJsonAsync(ConnectorUrl(""), new { });

        AssertReachedTheHandler(response);
    }

    /// <summary>Also rejects NotFound, so a feature gate swallowing the route does not pass as authorized.</summary>
    private static void AssertReachedTheHandler(HttpResponseMessage response)
    {
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}
