using System.Net;
using Bit.Api.Billing.Public.Models;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Xunit;

namespace Bit.Api.IntegrationTest.Billing.Public.Controllers;

public class ScopedOrganizationApiKeySubscriptionTests
    : IClassFixture<ScopedOrganizationApiKeysApiApplicationFactory>, IAsyncLifetime
{
    private readonly ScopedOrganizationApiKeysApiApplicationFactory _factory;
    private readonly HttpClient _client;

    private Organization _organization = null!;

    public ScopedOrganizationApiKeySubscriptionTests(ScopedOrganizationApiKeysApiApplicationFactory factory)
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
    public async Task SubscriptionReadToken_UpdateSubscription_ReturnsForbidden()
    {
        await new LoginHelper(_factory, _client)
            .LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationSubscriptionRead);
        var request = new OrganizationSubscriptionUpdateRequestModel
        {
            PasswordManager = new PasswordManagerSubscriptionUpdateModel { Seats = _organization.Seats + 1 },
        };

        var response = await _client.PutAsJsonAsync("public/organization/subscription", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MembersReadToken_GetSubscription_ReturnsForbidden()
    {
        await new LoginHelper(_factory, _client)
            .LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationMembersRead);

        var response = await _client.GetAsync("public/organization/subscription");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
