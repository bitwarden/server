using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
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
    public async Task Create_AsOwner_ReturnsCreated()
    {
        var request = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["acme.com", "example.com"],
            EncryptedInviteKey = _validEncryptedKey,
        };

        var response = await _client.PostAsJsonAsync(
            $"/organizations/{_organization.Id}/invite-link", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(content);
        Assert.NotEqual(Guid.Empty, content.Id);
        Assert.NotEqual(Guid.Empty, content.Code);
        Assert.Equal(_organization.Id, content.OrganizationId);
        Assert.Equal(["acme.com", "example.com"], content.AllowedDomains);
        Assert.Equal(_validEncryptedKey, content.EncryptedInviteKey);

        var repository = _factory.GetService<IOrganizationInviteLinkRepository>();
        var persisted = await repository.GetByOrganizationIdAsync(_organization.Id);
        Assert.NotNull(persisted);
        Assert.Equal(content.Id, persisted.Id);
    }
}
