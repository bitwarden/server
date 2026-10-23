using System.Net;
using System.Text.Json;
using Bit.Api.AdminConsole.Public.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.Dirt.Public.Controllers;

public class ScopedOrganizationApiKeyEventsTests
    : IClassFixture<ScopedOrganizationApiKeysApiApplicationFactory>, IAsyncLifetime
{
    private readonly ScopedOrganizationApiKeysApiApplicationFactory _factory;
    private readonly HttpClient _client;

    private Organization _organization = null!;

    public ScopedOrganizationApiKeyEventsTests(ScopedOrganizationApiKeysApiApplicationFactory factory)
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

        await new LoginHelper(_factory, _client)
            .LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationEventsRead);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task EventsReadToken_ListEvents_ReturnsOrganizationEvents()
    {
        var policyId = Guid.NewGuid();
        await _factory.GetService<IEventRepository>().CreateAsync(new Event
        {
            Type = EventType.Policy_Updated,
            OrganizationId = _organization.Id,
            PolicyId = policyId,
            Date = DateTime.UtcNow,
        });

        var response = await _client.GetAsync("public/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(result.GetProperty("data").EnumerateArray(),
            e => e.GetProperty("policyId").GetGuid() == policyId);
    }

    [Theory]
    [InlineData("public/members")]
    [InlineData("public/groups")]
    [InlineData("public/policies")]
    [InlineData("public/organization/subscription")]
    public async Task EventsReadToken_GetOtherPublicEndpoint_ReturnsForbidden(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EventsReadToken_Import_ReturnsForbidden()
    {
        var request = new OrganizationImportRequestModel
        {
            Groups = [],
            Members = [],
            OverwriteExisting = false,
        };

        var response = await _client.PostAsJsonAsync("public/organization/import", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
