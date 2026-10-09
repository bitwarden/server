using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Billing.Enums;
using Bit.Core.Context;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Xunit;

namespace Bit.Api.IntegrationTest.Auth;

public class ScopedOrganizationApiKeyTokenTests
    : IClassFixture<ScopedOrganizationApiKeysApiApplicationFactory>, IAsyncLifetime
{
    private readonly ScopedOrganizationApiKeysApiApplicationFactory _factory;
    private readonly HttpClient _client;

    private Organization _organization = null!;

    public ScopedOrganizationApiKeyTokenTests(ScopedOrganizationApiKeysApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: ownerEmail, passwordManagerSeats: 10, paymentMethod: PaymentMethodType.Card);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ScopedToken_LegacyOrganizationPolicyEndpoint_ReturnsForbidden()
    {
        await new LoginHelper(_factory, _client)
            .LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationMembersRead);

        var response = await _client.GetAsync("public/members");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ScopedToken_UserEndpoint_ReturnsForbidden()
    {
        await new LoginHelper(_factory, _client)
            .LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationMembersRead);

        var response = await _client.GetAsync($"organizations/{_organization.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ScopedToken_CurrentContext_ResolvesOrganization()
    {
        var token = await _factory.LoginWithScopedOrganizationApiKeyAsync(
            _organization.Id, ApiScopes.ApiOrganizationEventsRead);
        using var scope = _factory.Services.CreateScope();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Request.Headers.Authorization = $"Bearer {token}";

        var authentication = await httpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        Assert.True(authentication.Succeeded);
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContext>();
        await currentContext.SetContextAsync(authentication.Principal);

        Assert.Equal(_organization.Id, currentContext.OrganizationId);
        Assert.StartsWith($"organization.{_organization.Id}.", currentContext.ClientId);
    }
}
