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
/// Authorization for the <c>organizations/{orgId}/rotation</c> admin surface, exercised over the real request
/// pipeline.
/// </summary>
/// <remarks>
/// The endpoint-registration tests in Pam.Test assert that ManageRotationRequirement is attached to every rotation
/// admin route, but they stop before the pipeline runs — presence in metadata is not enforcement. These tests
/// deliberately assert only that a caller was or was not denied, never what the handler returned.
/// <para>
/// One route per group is enough: the requirement is applied once, to the parent group, so a route that escapes it
/// escapes it for the whole group.
/// </para>
/// </remarks>
public class RotationAuthorizationTests(ApiApplicationFactory factory)
    : AccessRuleIntegrationTestBase(factory, "pam-rotation-authz")
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // The rotation groups sit behind their own flag, on top of the base PAM flag the harness already enables.
        FeatureService.IsEnabled(FeatureFlagKeys.PamRotation).Returns(true);
    }

    private string RotationUrl(string group) => $"organizations/{Organization.Id}/rotation/{group}";

    [Theory]
    [InlineData("daemons")]
    [InlineData("target-systems")]
    [InlineData("configs")]
    public async Task Read_AsNonMember_ReturnsForbidden(string group)
    {
        var outsiderEmail = $"outsider-{Guid.NewGuid()}@bitwarden.com";
        await Factory.LoginWithNewAccount(outsiderEmail);
        await LoginHelper.LoginAsync(outsiderEmail);

        var response = await Client.GetAsync(RotationUrl(group));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("daemons")]
    [InlineData("target-systems")]
    [InlineData("configs")]
    public async Task Read_AsPlainMember_ReturnsForbidden(string group)
    {
        var (memberEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            Organization.Id, OrganizationUserType.User);
        await LoginHelper.LoginAsync(memberEmail);

        var response = await Client.GetAsync(RotationUrl(group));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("daemons")]
    [InlineData("target-systems")]
    [InlineData("configs")]
    public async Task Read_AsCustomUserWithManageAccessRules_ReturnsForbidden(string group)
    {
        // ManageAccessRules is authority over who may lease a credential, not over the daemons that rotate it.
        var (customEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            Organization.Id, OrganizationUserType.Custom, new Permissions { ManageAccessRules = true });
        await LoginHelper.LoginAsync(customEmail);

        var response = await Client.GetAsync(RotationUrl(group));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("daemons")]
    [InlineData("target-systems")]
    [InlineData("configs")]
    public async Task Read_AsProviderUserForTheOrganization_ReturnsForbidden(string group)
    {
        // A registered daemon is handed the organization key, which is not a provider's to hold.
        await LoginAsProviderForOrganizationAsync();

        var response = await Client.GetAsync(RotationUrl(group));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Write_AsPlainMember_ReturnsForbidden()
    {
        var (memberEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            Organization.Id, OrganizationUserType.User);
        await LoginHelper.LoginAsync(memberEmail);

        var response = await Client.PostAsJsonAsync(RotationUrl("daemons"), new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("daemons")]
    [InlineData("target-systems")]
    [InlineData("configs")]
    public async Task Read_AsOwner_IsNotForbidden(string group)
    {
        // Guards against the requirement over-denying.
        await LoginHelper.LoginAsync(OwnerEmail);

        var response = await Client.GetAsync(RotationUrl(group));

        AssertReachedTheHandler(response);
    }

    [Theory]
    [InlineData("daemons")]
    [InlineData("target-systems")]
    [InlineData("configs")]
    public async Task Read_AsAdmin_IsNotForbidden(string group)
    {
        var (adminEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            Organization.Id, OrganizationUserType.Admin);
        await LoginHelper.LoginAsync(adminEmail);

        var response = await Client.GetAsync(RotationUrl(group));

        AssertReachedTheHandler(response);
    }

    [Fact]
    public async Task Write_AsOwner_IsNotForbidden()
    {
        await LoginHelper.LoginAsync(OwnerEmail);

        var response = await Client.PostAsJsonAsync(RotationUrl("daemons"), new { });

        AssertReachedTheHandler(response);
    }

    /// <summary>
    /// Asserts a caller got past authorization without pinning what the handler did. NotFound is excluded as well as
    /// Forbidden: without it these would still pass if the rotation feature gate silently swallowed the route, which
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
