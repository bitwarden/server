using System.Net;
using System.Security.Claims;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Bit.Api.IntegrationTest.Auth;

public class OrganizationTokenAuthorizationTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _organizationClient;
    private readonly HttpClient _ownerClient;

    private Organization _organization = null!;

    public OrganizationTokenAuthorizationTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _organizationClient = factory.CreateClient();
        _ownerClient = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: ownerEmail, passwordManagerSeats: 10, paymentMethod: PaymentMethodType.Card);

        await new LoginHelper(_factory, _ownerClient).LoginAsync(ownerEmail);

        await new LoginHelper(_factory, _organizationClient).LoginWithOrganizationApiKeyAsync(_organization.Id);
    }

    public Task DisposeAsync()
    {
        _organizationClient.Dispose();
        _ownerClient.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(ApiScopes.Api)]
    [InlineData(ApiScopes.ApiSecrets)]
    public async Task SecretsPolicy_AcceptsApiAndSecretsScopes(string scope)
    {
        var result = await AuthorizeSecretsPolicyAsync(scope);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task SecretsPolicy_RejectsOrganizationScope()
    {
        var result = await AuthorizeSecretsPolicyAsync(ApiScopes.ApiOrganization);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task OrganizationToken_SecretsManagerEndpoint_ReturnsForbidden()
    {
        var response = await _organizationClient.GetAsync($"organizations/{_organization.Id}/secrets");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationToken_SponsorshipSyncStatus_ReturnsForbidden()
    {
        var response = await _organizationClient.GetAsync($"organization/sponsorship/{_organization.Id}/sync-status");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OwnerToken_SponsorshipSyncStatus_ReturnsOk()
    {
        var response = await _ownerClient.GetAsync($"organization/sponsorship/{_organization.Id}/sync-status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationToken_ActionWithoutOrganizationScopePolicy_ReturnsForbidden()
    {
        var response = await _organizationClient.GetAsync("config");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationToken_PublicApi_ReturnsOk()
    {
        var response = await _organizationClient.GetAsync("public/members");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<AuthorizationResult> AuthorizeSecretsPolicyAsync(string scope)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Scope, scope)], "Bearer"));
        return _factory.GetService<IAuthorizationService>().AuthorizeAsync(principal, Policies.Secrets);
    }
}
