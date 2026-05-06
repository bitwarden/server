using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
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

    private const string _validEncryptedKey =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

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
        _factory.SubstituteService<IApplicationCacheService>(cacheService =>
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
    public async Task CreateThenGet_AsOwner_ReturnsCreatedAndOk()
    {
        var request = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["acme.com", "example.com"],
            EncryptedInviteKey = _validEncryptedKey,
        };

        static void AssertInviteLink(OrganizationInviteLinkResponseModel? content, Organization organization)
        {
            Assert.NotNull(content);
            Assert.NotEqual(Guid.Empty, content.Id);
            Assert.NotEqual(Guid.Empty, content.Code);
            Assert.Equal(organization.Id, content.OrganizationId);
            Assert.Equal(["acme.com", "example.com"], content.AllowedDomains);
            Assert.Equal(_validEncryptedKey, content.EncryptedInviteKey);
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
            EncryptedInviteKey = _validEncryptedKey,
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
        Assert.Equal(_validEncryptedKey, updated.EncryptedInviteKey);
        Assert.Equal(["example.com", "new.com"], updated.AllowedDomains);

        var getResponse = await _client.GetAsync($"/organizations/{_organization.Id}/invite-link");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var content = await getResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(content);
        Assert.Equal(created.Id, content.Id);
        Assert.Equal(created.Code, content.Code);
        Assert.Equal(_validEncryptedKey, content.EncryptedInviteKey);
        Assert.Equal(["example.com", "new.com"], content.AllowedDomains);
    }
}
