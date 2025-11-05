using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
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

    public OrganizationsControllerTests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"org-integration-test-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually2023,
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
    public async Task Put_AsRegularOrganizationOwner_CanRenameOrganization()
    {
        // Arrange - Regular organization owner (no provider)
        await _loginHelper.LoginAsync(_ownerEmail);

        var updateRequest = new OrganizationUpdateRequestModel
        {
            Name = "Updated Organization Name",
            BusinessName = _organization.BusinessName,
            BillingEmail = _organization.BillingEmail
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
    }

    [Theory]
    [InlineData(ProviderType.Msp)]
    [InlineData(ProviderType.Reseller)]
    [InlineData(ProviderType.BusinessUnit)]
    public async Task Put_AsOrgOwnerWithProvider_CanRenameOrganization(ProviderType providerType)
    {
        // Arrange - Create provider and link to organization
        // The active user is ONLY an org owner, NOT a provider user
        await LinkOrganizationToProviderAsync(providerType);
        await _loginHelper.LoginAsync(_ownerEmail);

        var updateRequest = new OrganizationUpdateRequestModel
        {
            Name = $"{providerType} Provider Updated Name",
            BusinessName = _organization.BusinessName,
            BillingEmail = _organization.BillingEmail
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/organizations/{_organization.Id}", updateRequest);

        // Assert
        // This test SHOULD pass (expected behavior) but FAILS due to the bug
        // Expected: Organization owners should be able to rename their organization regardless of provider type
        // Actual: Returns 404 Not Found because changing the organization name is currently classed as a subscription
        // update which requires provider permissions if a provider is linked.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify the organization name was actually updated
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        var updatedOrg = await organizationRepository.GetByIdAsync(_organization.Id);
        Assert.NotNull(updatedOrg);
        Assert.Equal($"{providerType} Provider Updated Name", updatedOrg.Name);
    }

    /// <summary>
    /// Helper method to link the organization to a provider.
    /// This does NOT make any organization users into provider users.
    /// </summary>
    private async Task LinkOrganizationToProviderAsync(ProviderType providerType)
    {
        // Create the provider
        var providerRepository = _factory.GetService<IProviderRepository>();
        var provider = await providerRepository.CreateAsync(new Provider
        {
            Name = $"Test {providerType} Provider",
            BusinessName = $"Test {providerType} Provider Business",
            BillingEmail = $"provider-{providerType.ToString().ToLower()}@example.com",
            Type = providerType,
            Status = ProviderStatusType.Created,
            Enabled = true
        });

        // Link the provider to the organization
        var providerOrganizationRepository = _factory.GetService<IProviderOrganizationRepository>();
        await providerOrganizationRepository.CreateAsync(new ProviderOrganization
        {
            ProviderId = provider.Id,
            OrganizationId = _organization.Id,
            Key = "test-key"
        });
    }
}
