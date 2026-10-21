using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.KeyManagement.Controllers;

public class OrganizationKeyManagementControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _organizationKeyId = "0123456789abcdef0123456789abcdef";
    private const string _otherOrganizationKeyId = "fedcba9876543210fedcba9876543210";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IOrganizationRepository _organizationRepository;

    private string _ownerEmail = null!;
    private Organization _organization = null!;

    public OrganizationKeyManagementControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _organizationRepository = _factory.GetService<IOrganizationRepository>();
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            PlanType.EnterpriseAnnually, _ownerEmail, passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);
        _organization = organization;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PostOrganizationKeyIdAsync_MalformedKeyId_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.PostAsJsonAsync(OrganizationKeyIdUri(),
            new { OrganizationKeyId = "not-a-key-id" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertOrganizationKeyIdAsync(null);
    }

    [Theory]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task PostOrganizationKeyIdAsync_NotOwnerOrAdmin_ForbiddenAndKeyIdIsUnchanged(
        OrganizationUserType type)
    {
        var memberEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(memberEmail);
        await OrganizationTestHelpers.CreateUserAsync(_factory, _organization.Id, memberEmail, type,
            permissions: type == OrganizationUserType.Custom
                ? new Permissions { ManageResetPassword = true, ManageUsers = true }
                : null);
        await _loginHelper.LoginAsync(memberEmail);

        var response = await _client.PostAsJsonAsync(OrganizationKeyIdUri(), RequestFor(_organizationKeyId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertOrganizationKeyIdAsync(null);
    }

    [Fact]
    public async Task PostOrganizationKeyIdAsync_ProviderAdmin_ForbiddenAndKeyIdIsUnchanged()
    {
        var providerAdminEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(providerAdminEmail);
        var provider = await ProviderTestHelpers.CreateProviderAndLinkToOrganizationAsync(
            _factory, _organization.Id, ProviderType.Msp);
        await ProviderTestHelpers.CreateProviderUserAsync(_factory, provider.Id, providerAdminEmail,
            ProviderUserType.ProviderAdmin);
        await _loginHelper.LoginAsync(providerAdminEmail);

        var response = await _client.PostAsJsonAsync(OrganizationKeyIdUri(), RequestFor(_organizationKeyId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertOrganizationKeyIdAsync(null);
    }

    [Fact]
    public async Task PostOrganizationKeyIdAsync_Admin_StoresTheKeyId()
    {
        var adminEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(adminEmail);
        await OrganizationTestHelpers.CreateUserAsync(_factory, _organization.Id, adminEmail,
            OrganizationUserType.Admin);
        await _loginHelper.LoginAsync(adminEmail);

        var response = await _client.PostAsJsonAsync(OrganizationKeyIdUri(), RequestFor(_organizationKeyId));

        response.EnsureSuccessStatusCode();
        await AssertOrganizationKeyIdAsync(_organizationKeyId);
    }

    [Fact]
    public async Task PostOrganizationKeyIdAsync_Owner_BackfillsAndSyncReturnsTheKeyId()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var syncBefore = await _client.GetAsync("/sync");
        syncBefore.EnsureSuccessStatusCode();
        Assert.Contains("\"organizationKeyId\":null", await syncBefore.Content.ReadAsStringAsync());

        var response = await _client.PostAsJsonAsync(OrganizationKeyIdUri(), RequestFor(_organizationKeyId));

        response.EnsureSuccessStatusCode();
        await AssertOrganizationKeyIdAsync(_organizationKeyId);

        var syncAfter = await _client.GetAsync("/sync");
        syncAfter.EnsureSuccessStatusCode();
        Assert.Contains($"\"organizationKeyId\":\"{_organizationKeyId}\"",
            await syncAfter.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PostOrganizationKeyIdAsync_SameKeyIdTwice_SucceedsBothTimes()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var first = await _client.PostAsJsonAsync(OrganizationKeyIdUri(), RequestFor(_organizationKeyId));
        var second = await _client.PostAsJsonAsync(OrganizationKeyIdUri(), RequestFor(_organizationKeyId));

        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        await AssertOrganizationKeyIdAsync(_organizationKeyId);
    }

    [Fact]
    public async Task PostOrganizationKeyIdAsync_DifferentKeyIdAfterBackfill_BadRequestAndKeyIdIsUnchanged()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var first = await _client.PostAsJsonAsync(OrganizationKeyIdUri(), RequestFor(_organizationKeyId));
        first.EnsureSuccessStatusCode();

        var second = await _client.PostAsJsonAsync(OrganizationKeyIdUri(), RequestFor(_otherOrganizationKeyId));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        await AssertOrganizationKeyIdAsync(_organizationKeyId);
    }

    private string OrganizationKeyIdUri() =>
        $"/organizations/{_organization.Id}/key-management/organization-key-id";

    private static SetOrganizationKeyIdRequestModel RequestFor(string organizationKeyId) =>
        new() { OrganizationKeyId = organizationKeyId };

    private async Task AssertOrganizationKeyIdAsync(string? expected)
    {
        var organization = await _organizationRepository.GetByIdAsync(_organization.Id);
        Assert.NotNull(organization);
        Assert.Equal(expected, organization.OrganizationKeyId);
    }
}
