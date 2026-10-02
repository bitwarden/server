using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerBulkEnableSecretsManagerTests
    : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;
    private GlobalSettings _globalSettings = null!;

    private const int _smSeats = 1;

    public OrganizationUsersControllerBulkEnableSecretsManagerTests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _globalSettings = _factory.GetService<GlobalSettings>();
        _globalSettings.SelfHosted = false;

        _ownerEmail = $"sh-sm-enable-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, var owner) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually, ownerEmail: _ownerEmail, passwordManagerSeats: 5,
            paymentMethod: PaymentMethodType.Card);

        // Single SM seat, occupied by the owner: no headroom for another member.
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        _organization.UseSecretsManager = true;
        _organization.SmSeats = _smSeats;
        await organizationRepository.ReplaceAsync(_organization);

        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        owner.AccessSecretsManager = true;
        await organizationUserRepository.ReplaceAsync(owner);
    }

    [Fact]
    public async Task BulkEnableSecretsManager_WhenSelfHostedAndSeatsInsufficient_DoesNotChangeSmSeats()
    {
        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);

        await _loginHelper.LoginAsync(_ownerEmail);

        _globalSettings.SelfHosted = true;

        var request = new OrganizationUserBulkRequestModel { Ids = [member.Id] };

        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/enable-secrets-manager", request);

        // Self-host is rejected up front before any billing call, so seats are untouched.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Cannot autoscale on a self-hosted instance.", content);

        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        var reloadedOrganization = await organizationRepository.GetByIdAsync(_organization.Id);
        Assert.Equal(_smSeats, reloadedOrganization!.SmSeats);

        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var reloadedMember = await organizationUserRepository.GetByIdAsync(member.Id);
        Assert.False(reloadedMember!.AccessSecretsManager);
    }

    public Task DisposeAsync()
    {
        _globalSettings.SelfHosted = false;
        _client.Dispose();
        return Task.CompletedTask;
    }
}
