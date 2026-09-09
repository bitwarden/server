using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Xunit;

namespace Bit.Api.IntegrationTest.Billing.Controllers;

/// <summary>
/// Integration tests for PreviewInvoiceController, focusing on the organization-scoped
/// endpoints that take an organizationId in the route.
///
/// Reproduces VULN-501 (PM-34848): the [InjectOrganization] action filter loads an
/// Organization from the route but performs no membership/role check, so any
/// authenticated user could probe billing data for any organization.
/// </summary>
public class PreviewInvoiceControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private OrganizationUser _ownerOrgUser = null!;
    private string _ownerEmail = null!;

    public PreviewInvoiceControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"preview-invoice-owner-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _ownerOrgUser) = await OrganizationTestHelpers.SignUpAsync(
            _factory,
            plan: PlanType.Free,
            ownerEmail: _ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    #region plan-change authorization tests

    /// <summary>
    /// Reproduces VULN-501: a fully-unrelated authenticated user calls the plan-change
    /// preview endpoint with a victim org's id. Authorization must reject the request
    /// before [InjectOrganization] loads the org or the command reaches Stripe.
    /// </summary>
    [Fact]
    public async Task PreviewPlanChangeTax_AsNonMember_ReturnsForbidden()
    {
        var attackerEmail = $"preview-invoice-attacker-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(attackerEmail);
        await _loginHelper.LoginAsync(attackerEmail);

        var response = await _client.PostAsJsonAsync(
            $"billing/preview-invoice/organizations/{_organization.Id}/subscription/plan-change",
            BuildPlanChangePayload());

        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Expected 401 or 403 but got {(int)response.StatusCode} {response.StatusCode}. " +
            "Non-org-members must not be able to preview plan-change tax for an org they don't belong to.");
    }

    /// <summary>
    /// Cross-org variant: attacker owns their own org but is not a member of the victim's org.
    /// Owning a different org must not grant access to the victim's billing data.
    /// </summary>
    [Fact]
    public async Task PreviewPlanChangeTax_AsOwnerOfDifferentOrg_ReturnsForbidden()
    {
        var attackerEmail = $"preview-invoice-other-owner-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(attackerEmail);
        await OrganizationTestHelpers.SignUpAsync(
            _factory,
            plan: PlanType.Free,
            ownerEmail: attackerEmail,
            name: "Attacker Org",
            billingEmail: attackerEmail);

        await _loginHelper.LoginAsync(attackerEmail);

        var response = await _client.PostAsJsonAsync(
            $"billing/preview-invoice/organizations/{_organization.Id}/subscription/plan-change",
            BuildPlanChangePayload());

        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Expected 401 or 403 but got {(int)response.StatusCode} {response.StatusCode}. " +
            "Being an owner of a different org must not grant access to another org's billing data.");
    }

    /// <summary>
    /// A plain User of the org (not Owner, not Provider) is below the
    /// ManageOrganizationBillingRequirement bar and must be rejected.
    /// </summary>
    [Fact]
    public async Task PreviewPlanChangeTax_AsRegularMember_ReturnsForbidden()
    {
        var (memberEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User,
            permissions: new Permissions());
        await _loginHelper.LoginAsync(memberEmail);

        var response = await _client.PostAsJsonAsync(
            $"billing/preview-invoice/organizations/{_organization.Id}/subscription/plan-change",
            BuildPlanChangePayload());

        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Expected 401 or 403 but got {(int)response.StatusCode} {response.StatusCode}. " +
            "Regular org members must not be able to preview plan-change tax.");
    }

    /// <summary>
    /// Positive test: the org Owner satisfies the requirement and must pass authorization.
    /// The downstream call may fail (Stripe is not wired up in integration tests),
    /// but the response must NOT be 401/403.
    /// </summary>
    [Fact]
    public async Task PreviewPlanChangeTax_AsOwner_IsNotForbidden()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.PostAsJsonAsync(
            $"billing/preview-invoice/organizations/{_organization.Id}/subscription/plan-change",
            BuildPlanChangePayload());

        Assert.True(
            response.StatusCode is not HttpStatusCode.Forbidden and not HttpStatusCode.Unauthorized,
            $"Expected to pass authorization but got {(int)response.StatusCode} {response.StatusCode}.");
    }

    #endregion

    #region update authorization tests

    /// <summary>
    /// Reproduces the second half of VULN-501: the subscription/update preview endpoint
    /// is called by a non-member. Even though the endpoint itself returns BadRequest for
    /// orgs without an active Stripe subscription, that response leaks subscription
    /// status — authorization must reject the request first.
    /// </summary>
    [Fact]
    public async Task PreviewSubscriptionUpdateTax_AsNonMember_ReturnsForbidden()
    {
        var attackerEmail = $"preview-update-attacker-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(attackerEmail);
        await _loginHelper.LoginAsync(attackerEmail);

        var response = await _client.PutAsJsonAsync(
            $"billing/preview-invoice/organizations/{_organization.Id}/subscription/update",
            BuildUpdatePayload());

        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Expected 401 or 403 but got {(int)response.StatusCode} {response.StatusCode}. " +
            "Non-org-members must not be able to preview subscription updates for an org they don't belong to.");
    }

    [Fact]
    public async Task PreviewSubscriptionUpdateTax_AsOwnerOfDifferentOrg_ReturnsForbidden()
    {
        var attackerEmail = $"preview-update-other-owner-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(attackerEmail);
        await OrganizationTestHelpers.SignUpAsync(
            _factory,
            plan: PlanType.Free,
            ownerEmail: attackerEmail,
            name: "Attacker Org 2",
            billingEmail: attackerEmail);

        await _loginHelper.LoginAsync(attackerEmail);

        var response = await _client.PutAsJsonAsync(
            $"billing/preview-invoice/organizations/{_organization.Id}/subscription/update",
            BuildUpdatePayload());

        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Expected 401 or 403 but got {(int)response.StatusCode} {response.StatusCode}. " +
            "Being an owner of a different org must not grant access to another org's billing data.");
    }

    [Fact]
    public async Task PreviewSubscriptionUpdateTax_AsRegularMember_ReturnsForbidden()
    {
        var (memberEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User,
            permissions: new Permissions());
        await _loginHelper.LoginAsync(memberEmail);

        var response = await _client.PutAsJsonAsync(
            $"billing/preview-invoice/organizations/{_organization.Id}/subscription/update",
            BuildUpdatePayload());

        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Expected 401 or 403 but got {(int)response.StatusCode} {response.StatusCode}. " +
            "Regular org members must not be able to preview subscription updates.");
    }

    [Fact]
    public async Task PreviewSubscriptionUpdateTax_AsOwner_IsNotForbidden()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.PutAsJsonAsync(
            $"billing/preview-invoice/organizations/{_organization.Id}/subscription/update",
            BuildUpdatePayload());

        Assert.True(
            response.StatusCode is not HttpStatusCode.Forbidden and not HttpStatusCode.Unauthorized,
            $"Expected to pass authorization but got {(int)response.StatusCode} {response.StatusCode}.");
    }

    #endregion

    private static object BuildPlanChangePayload() => new
    {
        plan = new { tier = "Teams", cadence = "Annually" },
        billingAddress = new { country = "US", postalCode = "10001" }
    };

    private static object BuildUpdatePayload() => new
    {
        update = new { passwordManager = new { seats = 10 } }
    };
}
