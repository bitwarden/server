using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Settings;
using Xunit;

namespace Bit.Api.IntegrationTest.SecretsManager.Controllers;

public class ServiceAccountsControllerCreateSelfHostTests
    : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _mockEncryptedName =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;
    private GlobalSettings _globalSettings = null!;

    public ServiceAccountsControllerCreateSelfHostTests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _globalSettings = _factory.GetService<GlobalSettings>();
        _globalSettings.SelfHosted = false;

        _ownerEmail = $"sh-sa-create-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, var owner) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually, ownerEmail: _ownerEmail, passwordManagerSeats: 5,
            paymentMethod: PaymentMethodType.Card);

        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        _organization.UseSecretsManager = true;
        _organization.SmServiceAccounts = 0;
        await organizationRepository.ReplaceAsync(_organization);

        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        owner.AccessSecretsManager = true;
        await organizationUserRepository.ReplaceAsync(owner);
    }

    [Fact]
    public async Task Create_WhenSelfHostedAndSlotsInsufficient_DoesNotChangeServiceAccounts()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        _globalSettings.SelfHosted = true;

        var request = new ServiceAccountCreateRequestModel { Name = _mockEncryptedName };

        var response = await _client.PostAsJsonAsync(
            $"organizations/{_organization.Id}/service-accounts", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Cannot autoscale on a self-hosted instance.", content);

        var serviceAccountRepository = _factory.GetService<IServiceAccountRepository>();
        var serviceAccountCount =
            await serviceAccountRepository.GetServiceAccountCountByOrganizationIdAsync(_organization.Id);
        Assert.Equal(0, serviceAccountCount);

        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        var reloadedOrganization = await organizationRepository.GetByIdAsync(_organization.Id);
        Assert.Equal(0, reloadedOrganization!.SmServiceAccounts);
    }

    public Task DisposeAsync()
    {
        _globalSettings.SelfHosted = false;
        _client.Dispose();
        return Task.CompletedTask;
    }
}
