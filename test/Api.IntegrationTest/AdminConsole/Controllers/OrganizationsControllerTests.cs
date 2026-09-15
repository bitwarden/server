using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationsControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;
    private readonly string _billingEmail = "billing@example.com";
    private readonly string _organizationName = "Organizations Controller Test Org";

    public OrganizationsControllerTests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"org-integration-test-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            name: _organizationName,
            billingEmail: _billingEmail,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 5,
            paymentMethod: PaymentMethodType.Card);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Put_AsOwner_WithoutProvider_CanUpdateOrganization()
    {
        // Arrange - Regular organization owner (no provider)
        await _loginHelper.LoginAsync(_ownerEmail);

        var updateRequest = new OrganizationUpdateRequestModel
        {
            Name = "Updated Organization Name",
            BillingEmail = "newbillingemail@example.com"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/organizations/{_organization.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify the organization name was updated
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        var updatedOrg = await organizationRepository.GetByIdAsync(_organization.Id);
        Assert.NotNull(updatedOrg);
        Assert.Equal("Updated Organization Name", updatedOrg.Name);
        Assert.Equal("newbillingemail@example.com", updatedOrg.BillingEmail);
    }

    [Fact]
    public async Task Put_AsProvider_CanUpdateOrganization()
    {
        // Create and login as a new account to be the provider user (not the owner)
        var providerUserEmail = $"provider-{Guid.NewGuid()}@example.com";
        var (token, _) = await _factory.LoginWithNewAccount(providerUserEmail);

        // Set up provider linked to org and ProviderUser entry
        var provider = await ProviderTestHelpers.CreateProviderAndLinkToOrganizationAsync(_factory, _organization.Id,
            ProviderType.Msp);
        await ProviderTestHelpers.CreateProviderUserAsync(_factory, provider.Id, providerUserEmail,
            ProviderUserType.ProviderAdmin);

        await _loginHelper.LoginAsync(providerUserEmail);

        var updateRequest = new OrganizationUpdateRequestModel
        {
            Name = "Updated Organization Name",
            BillingEmail = "newbillingemail@example.com"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/organizations/{_organization.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify the organization name was updated
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        var updatedOrg = await organizationRepository.GetByIdAsync(_organization.Id);
        Assert.NotNull(updatedOrg);
        Assert.Equal("Updated Organization Name", updatedOrg.Name);
        Assert.Equal("newbillingemail@example.com", updatedOrg.BillingEmail);
    }

    [Fact]
    public async Task Put_NotMemberOrProvider_CannotUpdateOrganization()
    {
        // Create and login as a new account to be unrelated to the org
        var userEmail = "stranger@example.com";
        await _factory.LoginWithNewAccount(userEmail);
        await _loginHelper.LoginAsync(userEmail);

        var updateRequest = new OrganizationUpdateRequestModel
        {
            Name = "Updated Organization Name",
            BillingEmail = "newbillingemail@example.com"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/organizations/{_organization.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // Verify the organization name was not updated
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        var updatedOrg = await organizationRepository.GetByIdAsync(_organization.Id);
        Assert.NotNull(updatedOrg);
        Assert.Equal(_organizationName, updatedOrg.Name);
        Assert.Equal(_billingEmail, updatedOrg.BillingEmail);
    }

    [Fact]
    public async Task Put_AsOwner_WithProvider_CanRenameOrganization()
    {
        // Arrange - Create provider and link to organization
        // The active user is ONLY an org owner, NOT a provider user
        await ProviderTestHelpers.CreateProviderAndLinkToOrganizationAsync(_factory, _organization.Id, ProviderType.Msp);
        await _loginHelper.LoginAsync(_ownerEmail);

        var updateRequest = new OrganizationUpdateRequestModel
        {
            Name = "Updated Organization Name",
            BillingEmail = null
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/organizations/{_organization.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify the organization name was actually updated
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        var updatedOrg = await organizationRepository.GetByIdAsync(_organization.Id);
        Assert.NotNull(updatedOrg);
        Assert.Equal("Updated Organization Name", updatedOrg.Name);
        Assert.Equal(_billingEmail, updatedOrg.BillingEmail);
    }

    [Fact]
    public async Task Put_AsOwner_WithProvider_CannotChangeBillingEmail()
    {
        // Arrange - Create provider and link to organization
        // The active user is ONLY an org owner, NOT a provider user
        await ProviderTestHelpers.CreateProviderAndLinkToOrganizationAsync(_factory, _organization.Id, ProviderType.Msp);
        await _loginHelper.LoginAsync(_ownerEmail);

        var updateRequest = new OrganizationUpdateRequestModel
        {
            Name = "Updated Organization Name",
            BillingEmail = "updatedbilling@example.com"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/organizations/{_organization.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // Verify the organization was not updated
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        var updatedOrg = await organizationRepository.GetByIdAsync(_organization.Id);
        Assert.NotNull(updatedOrg);
        Assert.Equal(_organizationName, updatedOrg.Name);
        Assert.Equal(_billingEmail, updatedOrg.BillingEmail);
    }

    // PM-39004: routing behavior for org SSO identifiers containing characters that are not
    // URL path delimiters. These exercise the real HTTP pipeline (routing + URL decoding), which
    // the controller unit tests cannot, since they invoke the action method directly.

    [Fact]
    public async Task GetAutoEnrollStatus_IdentifierWithInnerSpace_ResolvesViaPathRoute()
    {
        // A space is not a path delimiter: it is percent-encoded to %20 within a single segment,
        // so the segment count is unchanged and the {identifier}/auto-enroll-status route matches.
        // A 200 proves the request routed to the action AND the org was resolved by this identifier.
        await _loginHelper.LoginAsync(_ownerEmail);
        const string identifier = "DQS bitwarden";
        await SetOrganizationIdentifierAsync(identifier);

        var response = await _client.GetAsync(
            $"/organizations/{Uri.EscapeDataString(identifier)}/auto-enroll-status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("Café")]            // accented Latin
    [InlineData("Straße")]          // German eszett
    [InlineData("Smørrebrød")]      // Danish ø
    [InlineData("価格表")]           // CJK
    [InlineData("Açaí Büro €")]     // mixed accents + euro sign + space
    public async Task GetAutoEnrollStatus_IdentifierWithNonAsciiCharacters_ResolvesViaPathRoute(
        string identifier)
    {
        // Non-ASCII characters are not path delimiters: they are UTF-8 percent-encoded within a
        // single segment (e.g. "Café" -> "Caf%C3%A9"), so the segment count is unchanged and the
        // {identifier}/auto-enroll-status route matches and resolves the org. A 200 proves it.
        await _loginHelper.LoginAsync(_ownerEmail);
        await SetOrganizationIdentifierAsync(identifier);

        var response = await _client.GetAsync(
            $"/organizations/{Uri.EscapeDataString(identifier)}/auto-enroll-status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAutoEnrollStatus_IdentifierWithSlash_FailsOnPathRouteButResolvesViaQueryRoute()
    {
        // Same org, same slash-bearing identifier, two transports:
        await _loginHelper.LoginAsync(_ownerEmail);
        const string identifier = "DQS/bitwarden";
        await SetOrganizationIdentifierAsync(identifier);

        // Path route: the "/" adds a segment, so /organizations/DQS/bitwarden/auto-enroll-status has
        // too many segments for {identifier}/auto-enroll-status -> no route matches -> 404, even
        // though an org with this exact identifier exists (proven by the query route below).
        var pathResponse = await _client.GetAsync("/organizations/DQS/bitwarden/auto-enroll-status");
        Assert.Equal(HttpStatusCode.NotFound, pathResponse.StatusCode);

        // Query route: the identifier travels outside the path, survives encoding, and resolves.
        var queryResponse = await _client.GetAsync(
            $"/organizations/auto-enroll-status?identifier={Uri.EscapeDataString(identifier)}");
        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
    }

    private async Task SetOrganizationIdentifierAsync(string identifier)
    {
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        _organization.Identifier = identifier;
        await organizationRepository.ReplaceAsync(_organization);
    }
}
