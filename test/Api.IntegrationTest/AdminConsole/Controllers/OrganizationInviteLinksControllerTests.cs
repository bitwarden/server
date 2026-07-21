using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationInviteLinksControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private const string _invite = "opaque-invite";

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public OrganizationInviteLinksControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService
                .IsEnabled(FeatureFlagKeys.GenerateInviteLink)
                .Returns(true);
        });
        _factory.SubstituteService<IOrganizationAbilityCacheService>(cacheService =>
        {
            cacheService
                .GetOrganizationAbilityAsync(Arg.Any<Guid>())
                .Returns(new OrganizationAbility { UseInviteLinks = true });
        });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"integration-test{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(
            _factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        await _loginHelper.LoginAsync(_ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ValidateEmailDomain_WithAllowedEmail_ReturnsIsAllowedTrue()
    {
        var createRequest = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["acme.com"],
            Invite = _invite,
            SupportsConfirmation = false,
        };
        var createResponse = await _client.PostAsJsonAsync(
            $"/organizations/{_organization.Id}/invite-link", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(created);

        var validateRequest = new OrganizationInviteLinkValidateEmailDomainRequestModel
        {
            Code = created.Code,
            Email = "user@acme.com",
        };
        using var anonymousClient = _factory.CreateClient();
        var validateResponse = await anonymousClient.PostAsJsonAsync(
            "/organizations/invite-link/validate-email-domain", validateRequest);

        Assert.Equal(HttpStatusCode.OK, validateResponse.StatusCode);
        var result = await validateResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkValidateEmailDomainResponseModel>();
        Assert.NotNull(result);
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task CreateThenGet_AsOwner_ReturnsCreatedAndOk()
    {
        var request = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["acme.com", "example.com"],
            Invite = _invite,
            SupportsConfirmation = true,
        };

        static void AssertInviteLink(OrganizationInviteLinkResponseModel? content, Organization organization)
        {
            Assert.NotNull(content);
            Assert.NotEqual(Guid.Empty, content.Id);
            Assert.NotEqual(Guid.Empty, content.Code);
            Assert.Equal(organization.Id, content.OrganizationId);
            Assert.Equal(["acme.com", "example.com"], content.AllowedDomains);
            Assert.Equal(_invite, content.Invite);
            Assert.True(content.SupportsConfirmation);
        }

        var createResponse = await _client.PostAsJsonAsync(
            $"/organizations/{_organization.Id}/invite-link", request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        AssertInviteLink(created, _organization);

        var getResponse = await _client.GetAsync($"/organizations/{_organization.Id}/invite-link");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var content = await getResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        AssertInviteLink(content, _organization);
    }

    [Fact]
    public async Task CreateThenUpdateThenGet_AsOwner_ReturnsCreatedAndOkAndOk()
    {
        var createRequest = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["acme.com"],
            Invite = _invite,
            SupportsConfirmation = false,
        };

        var createResponse = await _client.PostAsJsonAsync(
            $"/organizations/{_organization.Id}/invite-link", createRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(created);

        var updateRequest = new UpdateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["example.com", "new.com"],
        };

        var updateResponse = await _client.PutAsJsonAsync(
            $"/organizations/{_organization.Id}/invite-link", updateRequest);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(created.Code, updated.Code);
        Assert.Equal(_organization.Id, updated.OrganizationId);
        Assert.Equal(_invite, updated.Invite);
        Assert.Equal(["example.com", "new.com"], updated.AllowedDomains);

        var getResponse = await _client.GetAsync($"/organizations/{_organization.Id}/invite-link");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var content = await getResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(content);
        Assert.Equal(created.Id, content.Id);
        Assert.Equal(created.Code, content.Code);
        Assert.Equal(_invite, content.Invite);
        Assert.Equal(["example.com", "new.com"], content.AllowedDomains);
    }

    [Fact]
    public async Task UpdateInviteSupportConfirmThenGet_AsOwner_UpdatesOnlyInviteAndSupportsConfirmation()
    {
        // Arrange
        var createRequest = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["acme.com"],
            Invite = _invite,
            SupportsConfirmation = false,
        };

        var createResponse = await _client.PostAsJsonAsync(
            $"/organizations/{_organization.Id}/invite-link", createRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(created);

        const string updatedInvite = "updated-invite";
        var updateRequest = new UpdateInviteSupportConfirmRequestModel
        {
            Invite = updatedInvite,
            SupportsConfirmation = true,
        };

        // Act
        var updateResponse = await _client.PutAsJsonAsync(
            $"/organizations/{_organization.Id}/invite-link/support-confirm", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(created.Code, updated.Code);
        Assert.Equal(_organization.Id, updated.OrganizationId);
        Assert.Equal(updatedInvite, updated.Invite);
        Assert.True(updated.SupportsConfirmation);
        Assert.Equal(["acme.com"], updated.AllowedDomains);

        var getResponse = await _client.GetAsync($"/organizations/{_organization.Id}/invite-link");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var content = await getResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(content);
        Assert.Equal(created.Id, content.Id);
        Assert.Equal(created.Code, content.Code);
        Assert.Equal(updatedInvite, content.Invite);
        Assert.True(content.SupportsConfirmation);
        Assert.Equal(["acme.com"], content.AllowedDomains);
    }

    [Fact]
    public async Task Delete_AsOwner_ReturnsNoContentAndRemovesLink()
    {
        var createRequest = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["acme.com"],
            Invite = _invite,
            SupportsConfirmation = false,
        };
        var createResponse = await _client.PostAsJsonAsync(
            $"/organizations/{_organization.Id}/invite-link", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var deleteResponse = await _client.DeleteAsync(
            $"/organizations/{_organization.Id}/invite-link");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var deleteAgainResponse = await _client.DeleteAsync(
            $"/organizations/{_organization.Id}/invite-link");
        Assert.Equal(HttpStatusCode.NotFound, deleteAgainResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/organizations/{_organization.Id}/invite-link");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_AsOwner_ReplacesLink()
    {
        var createRequest = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["acme.com", "example.com"],
            Invite = _invite,
            SupportsConfirmation = false,
        };
        var createResponse = await _client.PostAsJsonAsync(
            $"/organizations/{_organization.Id}/invite-link", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var original = await createResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(original);

        var refreshRequest = new RefreshOrganizationInviteLinkRequestModel
        {
            Invite = _invite,
            SupportsConfirmation = false,
        };
        var refreshResponse = await _client.PostAsJsonAsync(
            $"/organizations/{_organization.Id}/invite-link/refresh", refreshRequest);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(refreshed);
        Assert.NotEqual(original.Id, refreshed.Id);
        Assert.NotEqual(original.Code, refreshed.Code);
        Assert.Equal(original.AllowedDomains, refreshed.AllowedDomains);
        Assert.Equal(_organization.Id, refreshed.OrganizationId);

        var getResponse = await _client.GetAsync($"/organizations/{_organization.Id}/invite-link");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var current = await getResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(current);
        Assert.Equal(refreshed.Id, current.Id);
    }

    [Fact]
    public async Task GetStatus_WithExistingLink_ReturnsData()
    {
        var createRequest = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["acme.com"],
            Invite = _invite,
            SupportsConfirmation = true,
        };
        var createResponse = await _client.PostAsJsonAsync(
            $"/organizations/{_organization.Id}/invite-link", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(created);

        var anonClient = _factory.CreateClient();
        var statusResponse = await anonClient.PostAsJsonAsync(
            "/organizations/invite-link/status",
            new GetOrganizationInviteLinkStatusRequestModel { Code = created.Code });

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var status = await statusResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkStatusResponseModel>();
        Assert.NotNull(status);
        Assert.Equal(_organization.Name, status.OrganizationName);
        Assert.True(status.LinksEnabled);
        Assert.True(status.SeatsAvailable);
        Assert.True(status.SupportsConfirmation);
    }

    [Fact]
    public async Task GetPolicies_WithExistingLink_ReturnsListResponseModel()
    {
        var createRequest = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["acme.com"],
            Invite = _invite,
            SupportsConfirmation = false,
        };
        var createResponse = await _client.PostAsJsonAsync(
            $"/organizations/{_organization.Id}/invite-link", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(created);

        var anonClient = _factory.CreateClient();
        var policiesResponse = await anonClient.PostAsJsonAsync(
            "/organizations/invite-link/policies",
            new GetOrganizationInviteLinkPoliciesRequestModel { Code = created.Code });

        Assert.Equal(HttpStatusCode.OK, policiesResponse.StatusCode);
        var body = await policiesResponse.Content.ReadFromJsonAsync<ListResponseModel<PolicyResponseModel>>();
        Assert.NotNull(body);
        Assert.NotNull(body.Data);
    }
}
