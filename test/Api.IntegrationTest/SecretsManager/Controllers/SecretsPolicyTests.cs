using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.IntegrationTest.SecretsManager.Helpers;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;
using AdminLoginHelper = Bit.Api.IntegrationTest.Helpers.LoginHelper;
using LoginHelper = Bit.Api.IntegrationTest.SecretsManager.Helpers.LoginHelper;

namespace Bit.Api.IntegrationTest.SecretsManager.Controllers;

/// <summary>
/// Covers the <c>Secrets</c> authorization policy that gates every Secrets Manager controller.
/// The policy must admit only the <c>api</c> and <c>api.secrets</c> scopes. A token carrying a
/// different <c>api.*</c> scope must be rejected at the policy, before any controller runs.
/// </summary>
public class SecretsPolicyTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly AdminLoginHelper _adminLoginHelper;
    private readonly LoginHelper _loginHelper;
    private readonly IOrganizationRepository _organizationRepository;

    private string _email = null!;
    private SecretsManagerOrganizationHelper _organizationHelper = null!;

    public SecretsPolicyTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _adminLoginHelper = new AdminLoginHelper(_factory, _client);
        _loginHelper = new LoginHelper(_factory, _client);
        _organizationRepository = _factory.GetService<IOrganizationRepository>();
    }

    public async Task InitializeAsync()
    {
        _email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_email);
        _organizationHelper = new SecretsManagerOrganizationHelper(_factory, _email);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// An organization API key token carries only the <c>api.organization</c> scope. Before the
    /// fix the policy matched that scope as a substring of <c>api</c> and let the request through
    /// to the controller, which then answered 404. The policy must reject it with 403 instead.
    /// </summary>
    [Fact]
    public async Task OrganizationApiKeyScope_IsRejectedByPolicy()
    {
        // The organization API key grant requires a plan with API access, so sign up on Enterprise
        // rather than through the Secrets Manager helper's Free-plan default.
        var (org, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: _email, billingEmail: _email, passwordManagerSeats: 10, paymentMethod: PaymentMethodType.Card);
        org.UseSecretsManager = true;
        await _organizationRepository.ReplaceAsync(org);

        await _adminLoginHelper.LoginWithOrganizationApiKeyAsync(org.Id);

        var response = await _client.GetAsync($"/organizations/{org.Id}/secrets");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserScope_IsAdmittedByPolicy()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var response = await _client.GetAsync($"/organizations/{org.Id}/secrets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SecretsScope_IsAdmittedByPolicy()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var apiKeyDetails = await _organizationHelper.CreateNewServiceAccountApiKeyAsync();
        await _loginHelper.LoginWithApiKeyAsync(apiKeyDetails);

        // A machine account with no project access is admitted by the policy and then told by the
        // controller that it cannot see the organization's secrets. Any 2xx or 404 proves the
        // policy let the api.secrets scope through; 403 would mean the policy rejected it.
        var response = await _client.GetAsync($"/organizations/{org.Id}/secrets");

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
