using System.Net.Http.Headers;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.Controllers;

// TODO Quartz jobs are conflicting when integration tests are ran in parallel. 
// For now sequently run integration tests.
[Collection("Sequential")]
public class ServiceAccountsControllerTest : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private Organization? _organization;

    public ServiceAccountsControllerTest(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _serviceAccountRepository = _factory.GetService<IServiceAccountRepository>();
    }

    public async Task InitializeAsync()
    {
        var ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        var tokens = await _factory.LoginWithNewAccount(ownerEmail);
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, ownerEmail: ownerEmail, billingEmail: ownerEmail);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
        _organization = organization;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetServiceAccountsByOrganization()
    {
        if (_organization == null)
        {
            throw new ArgumentNullException(nameof(_organization));
        }

        var serviceAccountsToCreate = 3;
        var serviceAccountIds = new List<Guid>();
        for (var i = 0; i < serviceAccountsToCreate; i++)
        {
            var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
            {
                OrganizationId = _organization.Id,
                Name = _mockEncryptedString,
            });
            serviceAccountIds.Add(serviceAccount.Id);
        }

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/service-accounts");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        var jsonResult = JsonDocument.Parse(content);

        Assert.NotEmpty(jsonResult.RootElement.GetProperty("data").EnumerateArray());
        Assert.Equal(serviceAccountIds.Count(), jsonResult.RootElement.GetProperty("data").EnumerateArray().Count());
    }
}
